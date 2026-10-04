using HarmonyLib;
using Sprocket.Vehicles;
using Sprocket.Vehicles.AmmunitionStorage;
using Sprocket.Vehicles.Cannons;
using Sprocket.Vehicles.CrewSystems;
using Sprocket.Vehicles.Weapons;
using Sprocket.WeaponFramework;
using UnityEngine;

namespace SprocketCarouselAutoloader;

internal static class MagazineRefill
{
    private static readonly HashSet<IntPtr> BusyCrew = new();
    private static int crewFrame = -1;
    private static readonly HashSet<IntPtr> WaitingTasks = new();
    private static readonly Dictionary<IntPtr, Vector3> OriginalCrewPoints = new();
    internal static bool Waiting(LoadTask task) => WaitingTasks.Contains(task.Pointer);
    private static int Count<T>(Il2CppSystem.Collections.Generic.IReadOnlyList<T> list) =>
        list.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<T>>().Count;

    private static Cannon? CannonFor(LoadTask task) => task.target?.TryCast<WeaponBehaviour>()?.mount?.TryCast<Cannon>();
    // Keep the native crew task and hand allocation; aim them at the magazine
    // being replenished instead of the mechanically operated cannon breech.
    internal static void UpdateCrewPoint(Cannon cannon)
    {
        var breech = FindBreech(cannon);
        var operable = breech?.loaderOperable;
        if (operable == null) return;
        var state = BustleRuntime.Racks(cannon).FirstOrDefault(s => BustleRuntime.Eligible(s, cannon));
        if (state == null) { RestoreCrewPoint(cannon); return; }
        if (!OriginalCrewPoints.ContainsKey(operable.Pointer)) OriginalCrewPoints[operable.Pointer] = operable.OperateLocalPosition;
        var world = state.Rack.transform.TransformPoint(state.Rack.LoadLocalPosition);
        var local = breech!.transform.InverseTransformPoint(world);
        if (Vector3.Distance(operable.OperateLocalPosition, local) > .001f)
        {
            operable.OperateLocalPosition = local;
            Plugin.ModLog.LogInfo($"[Bustle] Cannon {cannon.VUID.Value} crew loader now operates magazine {state.Rack.VUID.Value}; native hand reach retained.");
        }
    }
    private static void RestoreCrewPoint(Cannon cannon)
    {
        var operable = FindBreech(cannon)?.loaderOperable;
        if (operable != null && OriginalCrewPoints.Remove(operable.Pointer, out var point)) operable.OperateLocalPosition = point;
    }
    private static void Status(BustleState state, string status)
    {
        if (state.RefillStatus == status) return;
        state.RefillStatus = status;
        Plugin.ModLog.LogInfo($"[Bustle] Magazine {state.Rack.VUID.Value} refill: {status}.");
    }
    internal static IEnumerable<AmmoRack> Reserves(Cannon cannon)
    {
        var objects = cannon.Vehicle.ObjectReader.Items;
        for (int i = 0; i < Count(objects); i++)
        {
            var parts = objects[i].Components;
            for (int j = 0; j < Count(parts); j++)
            {
                var rack = parts[j].TryCast<AmmoRack>();
                if (rack != null && !BustleRuntime.IsPart(rack) && rack.IsInstalled && rack.HealthFraction > 0 &&
                    rack.ShellSizeBlueprintID == cannon.ShellSizeBlueprintID && rack.behaviour?.AmountStored > 0)
                    yield return rack;
            }
        }
    }

    // Do not let the native lookup bypass an empty magazine and feed the chamber directly.
    [HarmonyPrefix, HarmonyPriority(Priority.First), HarmonyPatch(typeof(LoadTask), nameof(LoadTask.UpdateRequest))]
    private static bool KeepMagazineRoute(LoadTask __instance)
    {
        try
        {
            var cannon = CannonFor(__instance);
            if (cannon == null) return true;
            if (Waiting(__instance)) return false;
            if (__instance.State == LoadState.Loading ||
                (__instance.State == LoadState.Loaded && __instance.currentRequest != LoadRequest.Consumed)) return true;
            var empty = BustleRuntime.Racks(cannon).FirstOrDefault(s => BustleRuntime.Eligible(s, cannon) && s.Rack.behaviour != null && s.Rack.behaviour.AmountStored == 0);
            if (empty == null) return true;
            empty.Refilling = true;
            WaitingTasks.Add(__instance.Pointer);
            // Consumed means already fired; Unload would incorrectly refund the round.
            if (__instance.State == LoadState.Loaded) __instance.SetState(LoadState.Unloaded);
            __instance.rack = empty.Rack.behaviour.Cast<IAmmoSource>();
            __instance.currentLoadInfo = empty.Rack.LoadInfo;
            __instance.currentRequest = LoadRequest.Consumed;
            // Native crew Contribute grants loader priority only for Loading.
            __instance.SetState(LoadState.Loading);
            Plugin.ModLog.LogInfo($"[Bustle] Cannon {cannon.VUID.Value} waiting for crew refill; direct reserve-to-chamber loading blocked.");
            return false;
        }
        catch (Exception ex) { Plugin.ModLog.LogWarning($"[Bustle] Magazine route: {ex.Message}"); return true; }
    }

    [HarmonyPrefix, HarmonyPriority(Priority.First), HarmonyPatch(typeof(LoadTask), nameof(LoadTask.Update))]
    private static bool Tick(LoadTask __instance, float __3)
    {
        CarouselRuntime.Guard("Crew magazine refill", () =>
        {
            var cannon = CannonFor(__instance);
            if (cannon == null || __3 <= 0 || !float.IsFinite(__3)) return;
            if (crewFrame != Time.frameCount) { crewFrame = Time.frameCount; BusyCrew.Clear(); }
            foreach (var state in BustleRuntime.Racks(cannon))
            {
                if (!BustleRuntime.Eligible(state, cannon)) { state.RefillProgress = 0; state.Refilling = false; continue; }
                var magazine = state.Rack.behaviour;
                if (magazine == null || state.RefillFrame == Time.frameCount) continue;
                state.RefillFrame = Time.frameCount;
                if (magazine.AmountStored == 0) state.Refilling = true;
                if (magazine.AmountStored >= magazine.Capacity) { state.Refilling = false; state.RefillProgress = 0; }
                if (!state.Refilling) continue;
                var breech = FindBreech(cannon);
                var operable = breech?.loaderOperable;
                var seat = operable?.AssignedOperator;
                var realTask = operable?.behaviour?.TryCast<LoadContributeTask>();
                // This is the real crew task, not the plugin's synthetic automatic contributor.
                if (seat?.ActiveBehaviour == null || !seat.ActiveBehaviour.Enabled || seat.ActiveBehaviour.HealthFraction <= 0)
                { Status(state, "no active healthy crew loader assigned"); continue; }
                if (!operable!.Valid) { Status(state, "native crew hand reach/assignment invalid"); continue; }
                if (realTask == null || realTask.operateCount == 0 || realTask.efficiency <= 0)
                { Status(state, "native crew loader not currently working"); continue; }
                if (BusyCrew.Contains(seat.Pointer)) continue;
                var source = Reserves(cannon).Where(r => r.behaviour.StoredTypeID.Value == magazine.StoredTypeID.Value)
                    .OrderBy(r => Vector3.Distance(r.transform.TransformPoint(r.LoadLocalPosition), state.Rack.transform.TransformPoint(state.Rack.LoadLocalPosition))).FirstOrDefault();
                if (source == null) { state.RefillProgress = 0; state.RefillSource = IntPtr.Zero; Status(state, "no matching reserve ammunition"); continue; }
                if (state.RefillSource != source.Pointer) { state.RefillSource = source.Pointer; state.RefillProgress = 0; }
                BusyCrew.Add(seat.Pointer);
                var path = new VehicleComponentPath(source, state.Rack);
                path.aLocalPosition = source.LoadLocalPosition; path.bLocalPosition = state.Rack.LoadLocalPosition; path.Update();
                var shot = source.LoadInfo;
                var technique = LoaderRules.GetLoadTechnique(ref shot);
                // Native command efficiency adds preparation time; it does not multiply
                // manipulation speed. A commander temporarily loading may have zero
                // command efficiency, which must not stop reserve transfer entirely.
                var seconds = Math.Max(.5, LoaderRules.CalculateLoadTime(technique, path.Distance, ref shot) +
                    LoaderRules.GetPrepareTime(realTask.commandEfficiency));
                Status(state, $"transferring from rack {source.VUID.Value}");
                state.RefillProgress += __3 * Math.Clamp(realTask.efficiency, 0, 1) / seconds;
                if (state.RefillProgress < 1 || magazine.AmountStored >= magazine.Capacity) continue;
                // Consume real reserve ammunition only on completed transfer; no stock is created.
                if (source.behaviour.TakeShell())
                {
                    var before = magazine.AmountStored;
                    magazine.LoadAmmo(1);
                    if (magazine.AmountStored != before + 1) source.behaviour.LoadAmmo(1);
                    else Plugin.ModLog.LogInfo($"[Bustle] Crew transferred one shot: rack {source.VUID.Value} -> magazine {state.Rack.VUID.Value}; ready stock {magazine.AmountStored}/{magazine.Capacity}.");
                }
                state.RefillProgress = 0;
            }
        });
        if (!Waiting(__instance)) return true;
        var assigned = CannonFor(__instance);
        var ready = assigned == null ? null : BustleRuntime.Racks(assigned).FirstOrDefault(s => BustleRuntime.Eligible(s, assigned));
        if (ready?.Rack.behaviour?.AmountStored > 0)
        {
            WaitingTasks.Remove(__instance.Pointer);
            __instance.rack = ready.Rack.behaviour.Cast<IAmmoSource>();
            __instance.SetState(LoadState.Unloaded);
            __instance.RequestReload(LoadRequest.Consumed);
            Plugin.ModLog.LogInfo($"[Bustle] Cannon {assigned!.VUID.Value} resumed mechanical loading from replenished magazine.");
            return true;
        }
        if (ready == null)
        {
            WaitingTasks.Remove(__instance.Pointer);
            __instance.rack = null;
            __instance.SetState(LoadState.Unloaded);
            return true;
        }
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(Cannon), nameof(Cannon.DisableBehaviour))]
    private static void ClearWaiting(Cannon __instance)
    {
        RestoreCrewPoint(__instance);
        var task = __instance.Behaviour?.LoadTask?.TryCast<LoadTask>();
        if (task != null) WaitingTasks.Remove(task.Pointer);
    }

    private static CannonBreech? FindBreech(Cannon cannon)
    {
        var objects = cannon.Vehicle.ObjectReader.Items;
        for (int i = 0; i < Count(objects); i++)
        {
            var parts = objects[i].Components;
            for (int j = 0; j < Count(parts); j++)
            {
                var breech = parts[j].TryCast<CannonBreech>();
                if (breech?.parentCannonVuid.Value == cannon.VUID.Value) return breech;
            }
        }
        return null;
    }
}
