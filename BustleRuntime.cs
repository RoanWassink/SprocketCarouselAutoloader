using HarmonyLib;
using Sprocket.Vehicles;
using Sprocket.Vehicles.AmmunitionStorage;
using Sprocket.Vehicles.Cannons;
using Sprocket.Vehicles.Weapons;
using Sprocket.WeaponFramework;
using Sprocket.DamageModelling;
using Il2CppSystem.Runtime.Serialization;

namespace SprocketCarouselAutoloader;

internal sealed class BustleState
{
    internal readonly AmmoRack Rack;
    internal bool Enabled = true;
    internal int CannonId = -1;
    internal VehicleComponentPath? Path;
    internal int PathCannonId = -1;
    internal bool Syncing;
    internal bool AutoAssign = true;
    internal float AddedMechanismMass;
    internal bool Refilling;
    internal double RefillProgress;
    internal IntPtr RefillSource;
    internal int RefillFrame = -1;
    internal string RefillStatus = "";
    internal BustleState(AmmoRack rack) => Rack = rack;
}

internal static class BustleRuntime
{
    internal const string PartGuid = "a4214ee4-45cf-42ab-80f7-5e6c389513a3";
    private const string EnabledKey = "roanBustleEnabledV1", CannonKey = "roanBustleCannonV1";
    private static readonly Dictionary<IntPtr, BustleState> States = new();
    [HarmonyPostfix, HarmonyPatch(typeof(Sprocket.Vehicles.PartImporting.PartDefinitionIO),
        nameof(Sprocket.Vehicles.PartImporting.PartDefinitionIO.DeserializePartDefinitionJSON))]
    private static void PartImported(Sprocket.PartImporting.PartDefinition __result)
    {
        if (__result?.guid == PartGuid)
            Plugin.ModLog.LogInfo($"[Bustle] Native part importer recognized '{__result.name}', {__result.components.Length} components, GUID {PartGuid}.");
    }
    private static int Count<T>(Il2CppSystem.Collections.Generic.IReadOnlyList<T> list) =>
        list.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<T>>().Count;
    internal static bool IsPart(AmmoRack rack) => rack.VehicleObject?.GUID == PartGuid;
    internal static BustleState State(AmmoRack rack)
    {
        if (!States.TryGetValue(rack.Pointer, out var state)) States[rack.Pointer] = state = new(rack);
        return state;
    }
    internal static IEnumerable<BustleState> Racks(Cannon cannon)
    {
        var objects = cannon.Vehicle.ObjectReader.Items;
        for (int i = 0; i < Count(objects); i++)
        {
            var parts = objects[i].Components;
            for (int j = 0; j < Count(parts); j++)
            {
                var rack = parts[j].TryCast<AmmoRack>();
                if (rack != null && rack.IsInstalled && IsPart(rack) && CarouselRuntime.SameTurret(rack, cannon))
                    yield return State(rack);
            }
        }
    }
    internal static bool Eligible(BustleState state, Cannon cannon) => state.Enabled && state.CannonId == cannon.VUID.Value &&
        state.Rack.IsInstalled && state.Rack.HealthFraction > 0 && state.Rack.Capacity > 0 &&
        state.Rack.ShellSizeBlueprintID == cannon.ShellSizeBlueprintID && CarouselRuntime.SameTurret(state.Rack, cannon) &&
        Distance(state, cannon) < MaximumDistance;
    internal static double MaximumDistance => CannonBreech.MinimumOperateHandDistance;
    internal static bool HasDesign(Cannon cannon) => Racks(cannon).Any(s => Eligible(s, cannon));
    internal static UnityEngine.Vector3 FeedLocalPosition(AmmoRack rack)
    {
        var outlet = BustleFeedGeometry.Outlet(rack.boundsSize.x, rack.boundsSize.z, rack.projectileSizeID.Caliber);
        return (rack.boundsCollider?.Centre ?? UnityEngine.Vector3.zero) +
            new UnityEngine.Vector3((float)outlet.X, (float)outlet.Y, (float)outlet.Z);
    }
    internal static double Distance(BustleState state, Cannon cannon)
    {
        if (state.Path == null || state.PathCannonId != cannon.VUID.Value)
        {
            state.Path = new VehicleComponentPath(state.Rack, cannon);
            state.PathCannonId = cannon.VUID.Value;
        }
        state.Path.aLocalPosition = FeedLocalPosition(state.Rack);
        state.Path.bLocalPosition = cannon.LoadLocalPosition;
        state.Path.Update();
        return state.Path.Distance;
    }
    internal static double Seconds(BustleState state, Cannon cannon) => BustleTiming.ReloadSeconds(
        cannon.ShellBlueprint.Diameter, cannon.ShellBlueprint.Mass, CarouselGeometry.FullRoundLength(cannon.ShellBlueprint.Diameter,
            cannon.ShellBlueprint.PropellantLength), Distance(state, cannon));

    internal static void Assign(BustleState state, Cannon? cannon)
    {
        state.CannonId = cannon?.VUID.Value ?? -1;
        state.AutoAssign = false;
        state.Path = null;
        if (cannon != null)
        {
            // One automatic feed per cannon: make the designer's most recent selection authoritative.
            foreach (var other in Racks(cannon))
                if (other.Rack.Pointer != state.Rack.Pointer && other.CannonId == state.CannonId)
                { other.CannonId = -1; other.AutoAssign = false; }
            foreach (var basket in CarouselRuntime.Baskets(cannon))
            {
                var carousel = CarouselRuntime.State(basket);
                if (carousel.CannonId != state.CannonId) continue;
                carousel.CannonId = -1;
                CarouselRuntime.Invalidate(carousel);
                basket.RequestRebuild();
                CarouselInspector.Redraw(basket);
            }
            Sync(state, cannon);
        }
        state.Rack.RequestRebuild();
    }
    private static void Sync(BustleState state, Cannon cannon)
    {
        if (state.Syncing || !state.Enabled) return;
        state.Syncing = true;
        try
        {
            var shell = cannon.ShellBlueprint;
            if (shell == null || shell.GeneratedProjectileBlueprint == null || Count(shell.GeneratedProjectileBlueprint) == 0) return;
            state.Rack.Blueprint.ShellSlotBlueprintID = cannon.ShellSizeBlueprintID;
            state.Rack.Blueprint.ShellTypeGuid = shell.GeneratedProjectileBlueprint[0].ProjectileTypeGuid;
            if (state.Rack.ShellSizeBlueprintID != cannon.ShellSizeBlueprintID)
                state.Rack.shellSlotBlueprintSlot.Load(cannon.ShellSizeBlueprintID);
            state.Rack.SyncWithShellSlotBlueprint(state.Rack.Blueprint, shell);
        }
        finally { state.Syncing = false; }
    }
    [HarmonyPrefix, HarmonyPatch(typeof(AmmoRack), nameof(AmmoRack.Build))]
    private static void PrepareRack(AmmoRack __instance)
    {
        if (!IsPart(__instance)) return;
        CarouselRuntime.Guard("Prepare bustle rack", () =>
        {
            var state = State(__instance);
            // Remove our previous contribution before vanilla rebuild, then add it to the resulting baseline.
            __instance.SetMass(Math.Max(0, __instance.GetMass(Sprocket.Vehicles.MassType.Mechanisms) - state.AddedMechanismMass),
                Sprocket.Vehicles.MassType.Mechanisms);
            state.AddedMechanismMass = 0;
            var cannons = CarouselRuntime.Cannons(__instance);
            if (state.AutoAssign && state.CannonId < 0 && cannons.Count == 1 &&
                !Racks(cannons[0]).Any(s => s.Rack.Pointer != __instance.Pointer && s.Enabled && s.CannonId == cannons[0].VUID.Value) &&
                !CarouselRuntime.Baskets(cannons[0]).Any(b => CarouselRuntime.State(b).Enabled && CarouselRuntime.State(b).CannonId == cannons[0].VUID.Value))
                state.CannonId = cannons[0].VUID.Value;
            if (cannons.Count > 0) state.AutoAssign = false;
            var cannon = cannons.FirstOrDefault(c => c.VUID.Value == state.CannonId);
            if (cannon != null) Sync(state, cannon);
        });
    }
    [HarmonyPostfix, HarmonyPatch(typeof(AmmoRack), nameof(AmmoRack.Build))]
    private static void RackMass(AmmoRack __instance)
    {
        if (!IsPart(__instance)) return;
        CarouselRuntime.Guard("Bustle mechanism mass", () =>
        {
            var state = State(__instance);
            state.AddedMechanismMass = state.Enabled ? (float)BustleTiming.MechanismMassKg(__instance.Capacity) : 0;
            __instance.SetMass(__instance.GetMass(Sprocket.Vehicles.MassType.Mechanisms) + state.AddedMechanismMass,
                Sprocket.Vehicles.MassType.Mechanisms);
        });
    }
    [HarmonyPostfix, HarmonyPatch(typeof(Cannon), nameof(Cannon.Build))]
    private static void CannonChanged(Cannon __instance)
    {
        CarouselRuntime.Guard("Refresh bustle ammunition", () =>
        {
            foreach (var state in Racks(__instance))
                if (state.Enabled && state.CannonId == __instance.VUID.Value)
                { Sync(state, __instance); state.Rack.RequestRebuild(); }
        });
    }
    internal static bool SelectSupply(LoadTask task, Cannon cannon)
    {
        if (task.State == LoadState.Loading || (task.State == LoadState.Loaded && task.currentRequest == LoadRequest.Unused)) return false;
        foreach (var state in Racks(cannon).OrderBy(s => s.Rack.VUID.Value))
        {
            if (!Eligible(state, cannon)) continue;
            var supply = state.Rack.behaviour;
            if (supply == null || supply.AmountStored == 0 || supply.StoredTypeID.Value == ProjectileTypeID.Invalid.Value) continue;
            var request = task.Request;
            if (request.TypeID.Value == ProjectileTypeID.Invalid.Value || request.SizeID.Caliber == 0)
            {
                request.TypeID = supply.StoredTypeID;
                request.SizeID = supply.ShellSizeID;
                request.Destination = cannon.LoadPosition;
            }
            request.Amount = 1;
            if (!request.IsMatch(supply.Cast<IAmmoSource>())) continue;
            task.Request = request;
            if (task.State == LoadState.NeverLoaded) task.currentRequest = LoadRequest.Unused;
            task.rack = supply.Cast<IAmmoSource>();
            task.currentLoadInfo = state.Rack.LoadInfo;
            SetTimings(task, state, cannon);
            return true;
        }
        return false;
    }
    internal static bool Mechanical(LoadTask task, Cannon? cannon, out BustleState state)
    {
        state = null!;
        if (cannon == null || task.rack == null) return false;
        foreach (var candidate in Racks(cannon))
        {
            if (!Eligible(candidate, cannon) || candidate.Rack.behaviour?.Pointer != task.rack.Pointer) continue;
            state = candidate;
            return true;
        }
        return false;
    }
    internal static bool Active(LoadTask task, Cannon? cannon) => Mechanical(task, cannon, out var state) &&
        (task.State == LoadState.Loading || task.State == LoadState.Loaded || state.Rack.behaviour.AmountStored > 0);
    internal static bool ApplySpeed(LoadTask task, ref float travelSpeed, ref float handlingSpeed, ref float ramTime)
    {
        var cannon = task.target?.TryCast<WeaponBehaviour>()?.mount?.TryCast<Cannon>();
        if (!Mechanical(task, cannon, out var state)) return false;
        var seconds = (float)Seconds(state, cannon!);
        travelSpeed = 1; handlingSpeed = 1; ramTime = seconds * .2f;
        return true;
    }
    internal static void ApplyTimings(LoadTask task)
    {
        var cannon = task.target?.TryCast<WeaponBehaviour>()?.mount?.TryCast<Cannon>();
        if (task.State == LoadState.Loading && Mechanical(task, cannon, out var state)) SetTimings(task, state, cannon!);
    }
    private static void SetTimings(LoadTask task, BustleState state, Cannon cannon)
    {
        var seconds = (float)Seconds(state, cannon);
        // Distance gates reach only; the established 20/20/40/20 cycle is retained.
        task.pickupTime = seconds * .2f;
        task.totalDistance = seconds * .2f;
        task.dropoffTime = seconds * .4f;
    }
    [HarmonyPostfix, HarmonyPatch(typeof(VehicleObjectSerialization), nameof(VehicleObjectSerialization.ToBlueprint))]
    private static void Save(VehicleObject __0, Sprocket.Vehicles.Serialization.VehicleObjectBlueprint __result)
    {
        CarouselRuntime.Guard("Save bustle rack", () =>
        {
            var parts = __0.Components;
            for (int i = 0; i < Count(parts); i++)
            {
                var rack = parts[i].TryCast<AmmoRack>();
                if (rack == null || !IsPart(rack)) continue;
                var state = State(rack);
                WriteSettings(__result.State, state.Enabled, state.CannonId);
            }
        });
    }
    [HarmonyPostfix, HarmonyPatch(typeof(VehicleComponent), nameof(VehicleComponent.LoadData))]
    private static void Load(VehicleComponent __instance, SerializationInfo __0)
    {
        var rack = __instance.TryCast<AmmoRack>();
        if (rack == null || !IsPart(rack)) return;
        CarouselRuntime.Guard("Restore bustle rack", () =>
        {
            var state = State(rack);
            var saved = ReadSettings(__0);
            state.Enabled = saved.Enabled;
            state.CannonId = saved.CannonId;
            state.AutoAssign = !saved.HasAssignment;
            state.Path = null;
        });
    }
    internal static void WriteSettings(SerializationInfo info, bool enabled, int cannonId)
    { info.AddValue(EnabledKey, enabled); info.AddValue(CannonKey, cannonId); }
    internal static (bool Enabled, int CannonId, bool HasAssignment) ReadSettings(SerializationInfo info)
    {
        var keys = new HashSet<string>(); var entries = info.GetEnumerator();
        while (entries.MoveNext()) keys.Add(entries.Name);
        return (!keys.Contains(EnabledKey) || info.GetBoolean(EnabledKey),
            keys.Contains(CannonKey) ? info.GetInt32(CannonKey) : -1, keys.Contains(CannonKey));
    }
    [HarmonyPrefix, HarmonyPatch(typeof(VehicleComponent), nameof(VehicleComponent.ReleaseInternal))]
    private static void Release(VehicleComponent __instance) => States.Remove(__instance.Pointer);
}
