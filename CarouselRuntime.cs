using HarmonyLib;
using Sprocket;
using Sprocket.Vehicles;
using Sprocket.Vehicles.Turrets;
using Sprocket.Vehicles.Cannons;
using Sprocket.Vehicles.AmmunitionStorage;
using Sprocket.Vehicles.Weapons;
using Sprocket.WeaponFramework;
using Sprocket.DamageModelling;
using Sprocket.Vehicles.CrewSystems;
using UnityEngine;

namespace SprocketCarouselAutoloader;

internal sealed class BasketState
{
    internal readonly TurretBasket Basket;
    internal bool Enabled;
    internal int CannonId = -1;
    internal CarouselLayout Layout;
    internal AmmoRackBehaviour? Rack;
    internal CarouselSize Size;
    internal int ShellId = -1;
    internal string LastPreview = "";
    internal IProjectileTypeRegister? Register;
    internal Il2CppSystem.Guid RegisteredGuid;
    internal bool HasRegistration;
    internal BasketState(TurretBasket basket) => Basket = basket;
}

internal static class CarouselRuntime
{
    private static int Count<T>(Il2CppSystem.Collections.Generic.IReadOnlyList<T> list) =>
        list.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<T>>().Count;
    private static readonly Dictionary<IntPtr, BasketState> States = new();
    private static readonly Dictionary<IntPtr, BasketState> Racks = new();
    private sealed record AutoLoaderBinding(Cannon Cannon, LoadTask Task, LoadContributeTask Contributor);
    private static readonly Dictionary<IntPtr, AutoLoaderBinding> AutoLoaders = new();
    internal static BasketState State(TurretBasket basket)
    {
        if (!States.TryGetValue(basket.Pointer, out var state)) States[basket.Pointer] = state = new(basket);
        return state;
    }
    internal static void Guard(string operation, Action action)
    {
        try { action(); }
        catch (Exception ex) { Plugin.ModLog.LogError($"[Carousel] {operation}: {ex}"); }
    }
    internal static IEnumerable<TurretBasket> Baskets(Cannon cannon)
    {
        var objects = cannon.Vehicle.ObjectReader.Items;
        for (int i = 0; i < Count(objects); i++)
        {
            var components = objects[i].Components;
            for (int j = 0; j < Count(components); j++)
            {
                var basket = components[j].TryCast<TurretBasket>();
                if (basket != null && basket.IsInstalled && SameTurret(basket, cannon)) yield return basket;
            }
        }
    }
    private static VehicleObject? Turret(VehicleComponent component)
    {
        var t = component.VehicleTransform;
        // A turret ring/basket is on the owning turret object. Stop at the nearest basket.
        for (int depth = 0; t != null && depth < 128; depth++, t = t.Parent)
        {
            var parts = t.VehicleObject.Components;
            for (int i = 0; i < Count(parts); i++)
                if (parts[i].TryCast<TurretBasket>() != null) return t.VehicleObject;
        }
        return null;
    }
    internal static bool SameTurret(TurretBasket basket, Cannon cannon)
    {
        var a = Turret(basket); var b = Turret(cannon);
        return a != null && b != null && a.Pointer == b.Pointer;
    }
    internal static List<Cannon> Cannons(TurretBasket basket)
    {
        var result = new List<Cannon>();
        var objects = basket.Vehicle.ObjectReader.Items;
        for (int i = 0; i < Count(objects); i++)
        {
            var parts = objects[i].Components;
            for (int j = 0; j < Count(parts); j++)
            {
                var cannon = parts[j].TryCast<Cannon>();
                if (cannon != null && cannon.IsInstalled && SameTurret(basket, cannon)) result.Add(cannon);
            }
        }
        return result.OrderBy(c => c.VUID.Value).ToList();
    }
    internal static CarouselSize Calculate(TurretBasket basket, Cannon cannon)
    {
        var bp = basket.BlueprintSlot.Blueprint;
        var scale = basket.Scale;
        var segments = new List<Segment>();
        if (bp?.Segments != null)
            foreach (var segment in bp.Segments)
                segments.Add(new(segment.Diameter, segment.Depth));
        segments = CarouselGeometry.ResolveBasket(segments, basket.ringDiameter)
            .Select(s => new Segment(s.DiameterMm * Math.Min(Math.Abs(scale.x), Math.Abs(scale.z)), s.DepthMm * Math.Abs(scale.y))).ToList();
        var shell = cannon.shellBlueprintSlot?.Blueprint;
        return shell == null ? new(0, 0, 0, 0, 0, 0, "Ammunition blueprint is not ready") :
            CarouselGeometry.Calculate(segments, shell.Diameter,
                CarouselGeometry.FullRoundLength(shell.Diameter, shell.PropellantLength), shell.Mass, State(basket).Layout);
    }
    internal static void Invalidate(BasketState state)
    {
        if (state.Rack != null) Racks.Remove(state.Rack.Pointer);
        state.Rack = null;
        state.ShellId = -1;
    }
    private static void ReleaseRegistration(BasketState state)
    {
        if (state.HasRegistration && state.Register != null) state.Register.RemoveUser(state.RegisteredGuid);
        state.HasRegistration = false;
        state.Register = null;
    }
    private static bool InitializeRequest(LoadTask task, Cannon cannon, BasketState state, ref AmmoRequest request)
    {
        if (request.TypeID.Value != ProjectileTypeID.Invalid.Value && request.SizeID.Caliber > 0) return true;
        if (Calculate(state.Basket, cannon).Capacity == 0) return false;
        var shell = cannon.ShellBlueprint;
        var types = shell.GeneratedProjectileBlueprint;
        var register = IProjectileTypeRegister.Instance;
        if (register == null || types == null || Count(types) == 0) return false;
        var projectile = types[0]; // Native AP base; the cannon's Shell Selector profile overrides firing as usual.
        var guid = projectile.ProjectileTypeGuid;
        if (!state.HasRegistration || state.Register?.Pointer != register.Pointer || !state.RegisteredGuid.Equals(guid))
        {
            ReleaseRegistration(state);
            if (!register.IsRegistered(guid))
                register.Create($"{shell.Diameter} mm carousel", guid, MathF.Pow(shell.Diameter, 3) * 1.58999992e-5f,
                    shell.Diameter * .001f, shell.Diameter * .003f, 1f, 10f, projectile.FunctionDefinitions);
            register.AddUser(guid);
            state.Register = register;
            state.RegisteredGuid = guid;
            state.HasRegistration = true;
        }
        request.TypeID = register.GetID(guid); // Only after verified registration, never a speculative GUID lookup.
        request.SizeID = ProjectileSizeID.FromPropellantLength(shell.Diameter, shell.PropellantLength);
        request.Amount = 1;
        request.Destination = cannon.LoadPosition;
        task.Request = request; // Write the plain struct back to the native task.
        if (task.State == LoadState.NeverLoaded) task.currentRequest = LoadRequest.Unused;
        Plugin.ModLog.LogInfo($"[Carousel] Initialized rack-independent request: cannon {cannon.VUID.Value}, type {request.TypeID.Value}, caliber {request.SizeID.Caliber}, propellant {request.SizeID.PropellantLength}");
        return true;
    }
    [HarmonyPostfix, HarmonyPatch(typeof(LoadTask), MethodType.Constructor, new[] { typeof(IAmmoSourceLookup), typeof(ILoadable) })]
    private static void InitializeTask(LoadTask __instance) => SelectSupply(__instance);
    private static AmmoRackBehaviour? Supply(BasketState state, Cannon cannon, ProjectileSizeID actualSize, ProjectileTypeID type)
    {
        var size = Calculate(state.Basket, cannon);
        state.Size = size;
        if (size.Capacity == 0 || !state.Enabled || !state.Basket.IsInstalled || state.Basket.HealthFraction <= 0) return null;
        // The native request is authoritative in combat. Never accept a differently sized round.
        if (actualSize.Caliber != cannon.ShellBlueprint.Diameter ||
            actualSize.PropellantLength != cannon.ShellBlueprint.PropellantLength) return null;
        if (type.Value == ProjectileTypeID.Invalid.Value) return null;
        if (state.Rack != null && state.Rack.ShellSizeID.Value == actualSize.Value && state.Rack.StoredTypeID.Value == type.Value)
        {
            // Rebuilding or shrinking during play must never manufacture replacement rounds.
            state.Rack.Capacity = (ushort)size.Capacity;
            if (state.Rack.AmountStored > size.Capacity) state.Rack.amountStored = (ushort)size.Capacity;
            return state.Rack;
        }
        // The cannon's native load request already contains the registered runtime type.
        // Blueprint GUIDs can be stale after registration/profile overrides. Do not resolve them again.
        var oldAmount = state.Rack?.AmountStored;
        Invalidate(state);
        state.Rack = new AmmoRackBehaviour($"Carousel {state.Basket.VUID.Value}", type,
            actualSize, size.Capacity);
        if (oldAmount.HasValue) state.Rack.amountStored = (ushort)Math.Min(oldAmount.Value, size.Capacity);
        else state.Rack.FullyLoad();
        state.ShellId = cannon.ShellSizeBlueprintID;
        Racks[state.Rack.Pointer] = state;
        Plugin.ModLog.LogInfo($"[Carousel] Basket {state.Basket.VUID.Value}, cannon {cannon.VUID.Value}: {size.Capacity} rounds, {size.MaxLengthMm:0} mm limit, {size.ReloadSeconds:0.0} s reload");
        return state.Rack;
    }
    private static bool Mechanical(LoadTask task, out BasketState state)
    {
        state = null!;
        return task.rack != null && Racks.TryGetValue(task.rack.Pointer, out state!) && state.Enabled &&
            state.Basket.IsInstalled && state.Basket.HealthFraction > 0;
    }
    private static bool ReplacesLoader(VehicleOperable operable)
    {
        // Loader roles belong to the breech, unlike gunner roles on the cannon.
        var breech = operable.Component?.TryCast<CannonBreech>();
        if (breech == null || breech.loaderOperable?.Pointer != operable.Pointer) return false;
        var objects = breech.Vehicle.ObjectReader.Items;
        for (int i = 0; i < Count(objects); i++)
        {
            var parts = objects[i].Components;
            for (int j = 0; j < Count(parts); j++)
            {
                var cannon = parts[j].TryCast<Cannon>();
                if (cannon == null || cannon.VUID.Value != breech.parentCannonVuid.Value || !cannon.IsInstalled) continue;
                foreach (var basket in Baskets(cannon))
                {
                    var state = State(basket);
                    if (state.Enabled && state.CannonId == cannon.VUID.Value && basket.HealthFraction > 0 && Calculate(basket, cannon).Capacity > 0)
                        return true;
                }
            }
        }
        return false;
    }
    [HarmonyPrefix, HarmonyPatch(typeof(AllOperablesValidCriterion), nameof(AllOperablesValidCriterion.Validate))]
    private static void ValidateAutomaticLoaders(AllOperablesValidCriterion __instance,
        out Il2CppSystem.Collections.Generic.IEnumerable<VehicleOperable>? __state)
    {
        __state = null;
        try
        {
            var original = __instance.operables;
            var kept = new Il2CppSystem.Collections.Generic.List<VehicleOperable>();
            // Copy before inspecting components: lazy component access can modify the source crew registry.
            var snapshot = Il2CppSystem.Linq.Enumerable.ToArray<VehicleOperable>(original);
            bool changed = false;
            foreach (var operable in snapshot)
            {
                if (ReplacesLoader(operable)) changed = true;
                else kept.Add(operable);
            }
            if (!changed) return;
            __state = original;
            __instance.operables = kept.Cast<Il2CppSystem.Collections.Generic.IEnumerable<VehicleOperable>>();
        }
        catch (Exception ex) { Plugin.ModLog.LogError($"[Carousel] Automatic-loader validation: {ex}"); }
    }
    [HarmonyPostfix, HarmonyPatch(typeof(Cannon), nameof(Cannon.EnableBehaviour))]
    private static void AttachNativeLoader(Cannon __instance)
    {
        Guard("Attach native automatic loader", () =>
        {
            var weapon = __instance.Behaviour;
            var task = weapon?.LoadTask?.TryCast<LoadTask>();
            if (weapon == null || task == null) return;
            if (!Baskets(__instance).Any(b =>
            {
                var s = State(b);
                return s.Enabled && s.CannonId == __instance.VUID.Value && b.HealthFraction > 0 && Calculate(b, __instance).Capacity > 0;
            })) return;
            SelectSupply(task);
            var exists = AutoLoaders.TryGetValue(__instance.Pointer, out var binding) && binding.Task.Pointer == task.Pointer;
            var contributor = exists ? binding!.Contributor : new LoadContributeTask { efficiency = 1f, commandEfficiency = 1f, operateCount = 1 };
            var previous = weapon.LoadTaskContributors;
            if (previous != null && previous.Any(c => c.Pointer == contributor.Pointer)) return;
            var appended = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<ILoadTaskContributor>((previous?.Length ?? 0) + 1);
            if (previous != null) for (int i = 0; i < previous.Length; i++) appended[i] = previous[i];
            appended[appended.Length - 1] = contributor.Cast<ILoadTaskContributor>();
            weapon.LoadTaskContributors = appended;
            AutoLoaders[__instance.Pointer] = new(__instance, task, contributor);
            Plugin.ModLog.LogInfo($"[Carousel] Native automatic loader attached to cannon {__instance.VUID.Value}; no crew-seat role required.");
        });
    }
    [HarmonyPrefix, HarmonyPatch(typeof(VehicleDesigner.AmmunitionStorage.WeaponLoadControllerAssemblyStage),
        nameof(VehicleDesigner.AmmunitionStorage.WeaponLoadControllerAssemblyStage.Execute))]
    private static void AttachBeforeControllerAssembly(VehicleDesigner.AmmunitionStorage.WeaponLoadControllerAssemblyStage __instance)
    {
        // Crew assembly may replace the breech contributor array after Cannon.EnableBehaviour.
        // The controller captures this array, so ensure our contributor is present before that capture.
        Guard("Assemble automatic loaders", () =>
        {
            foreach (var source in __instance.sources)
            {
                var cannon = source.TryCast<Cannon>();
                if (cannon != null) AttachNativeLoader(cannon);
            }
        });
    }
    [HarmonyPrefix, HarmonyPatch(typeof(VehicleWeaponLoadController), nameof(VehicleWeaponLoadController.Update))]
    private static void RefreshNativeLoaders()
    {
        Guard("Refresh native automatic loaders", () =>
        {
            foreach (var binding in AutoLoaders.Values.ToArray())
            {
                var task = binding.Task;
                if (task.State != LoadState.Loading && task.State != LoadState.Loaded) SelectSupply(task);
                var active = AutomaticSourceActive(task, binding.Cannon);
                binding.Contributor.efficiency = active ? 1f : 0f;
                binding.Contributor.commandEfficiency = active ? 1f : 0f;
                binding.Contributor.operateCount = active ? (byte)1 : (byte)0;
            }
        });
    }
    [HarmonyPrefix, HarmonyPatch(typeof(Cannon), nameof(Cannon.DisableBehaviour))]
    private static void DetachNativeLoader(Cannon __instance) => AutoLoaders.Remove(__instance.Pointer);
    [HarmonyFinalizer, HarmonyPatch(typeof(AllOperablesValidCriterion), nameof(AllOperablesValidCriterion.Validate))]
    private static void RestoreOperables(AllOperablesValidCriterion __instance,
        Il2CppSystem.Collections.Generic.IEnumerable<VehicleOperable>? __state)
    {
        if (__state != null) __instance.operables = __state;
    }
    private static bool AutomaticSourceActive(LoadTask task, Cannon? cannon)
    {
        if (Mechanical(task, out var state))
            return task.State == LoadState.Loading || task.State == LoadState.Loaded || state.Rack?.AmountStored > 0;
        // Physics activation can invalidate the supply cache after the initial chamber load.
        if (task.State != LoadState.Loaded || task.rack == null || cannon == null) return false;
        return Baskets(cannon).Any(basket =>
        {
            var configured = State(basket);
            return configured.Enabled && configured.CannonId == cannon.VUID.Value && basket.HealthFraction > 0 &&
                task.rack.RackIdentifier == $"Carousel {basket.VUID.Value}" && Calculate(basket, cannon).Capacity > 0;
        });
    }
    [HarmonyPrefix, HarmonyPatch(typeof(WeaponBehaviour), nameof(WeaponBehaviour.LoaderEnableFraction), MethodType.Getter)]
    private static bool AutomaticLoaderEnabled(WeaponBehaviour __instance, ref float __result)
    {
        try
        {
            var task = __instance.LoadTask?.TryCast<LoadTask>();
            if (task == null) return true;
            var cannon = __instance.mount?.TryCast<Cannon>();
            if (!AutomaticSourceActive(task, cannon))
            {
                // Native LoadContributeTask.EnableFraction is constant 1 even with efficiency 0.
                // Exclude the inactive synthetic task when reporting manual-loader availability.
                if (cannon == null || !AutoLoaders.TryGetValue(cannon.Pointer, out var binding)) return true;
                float total = 0f;
                int count = 0;
                var contributors = __instance.LoadTaskContributors;
                if (contributors != null) foreach (var contributor in contributors)
                {
                    if (contributor.Pointer == binding.Contributor.Pointer) continue;
                    total += contributor.EnableFraction;
                    count++;
                }
                __result = count == 0 ? 0f : total / count;
                return false;
            }
            __result = 1f;
            return false;
        }
        catch (Exception ex) { Plugin.ModLog.LogError($"[Carousel] Automatic loader enabled: {ex}"); return true; }
    }

    // RackPath is a boxed IL2CPP value type. Do not detour its by-ref output:
    // inject a native IAmmoSource reference before the native request selects a rack.
    [HarmonyPrefix, HarmonyPatch(typeof(LoadTask), nameof(LoadTask.UpdateRequest))]
    private static void SelectSupply(LoadTask __instance)
    {
        try
        {
            // LoadInstantly uses Unused for initial chamber loading too. Only an
            // already-loaded Unused request is an unload/type-switch operation.
            if (__instance.State == LoadState.Loading ||
                (__instance.State == LoadState.Loaded && __instance.currentRequest == LoadRequest.Unused)) return;
            var cannon = __instance.target?.TryCast<WeaponBehaviour>()?.mount?.TryCast<Cannon>();
            if (cannon == null) return;
            var request = __instance.Request;
            request.Amount = 1;
            foreach (var basket in Baskets(cannon))
            {
                var state = State(basket);
                if (!state.Enabled || state.CannonId != cannon.VUID.Value) continue;
                if (!InitializeRequest(__instance, cannon, state, ref request)) continue;
                var rack = Supply(state, cannon, request.SizeID, request.TypeID);
                if (rack == null || !request.IsMatch(rack.Cast<IAmmoSource>())) continue;
                var shell = cannon.ShellBlueprint;
                __instance.rack = rack.Cast<IAmmoSource>();
                __instance.currentLoadInfo = new ShellLoadInfo(shell.ProjectileMass, shell.PropellantMass,
                    (float)CarouselGeometry.FullRoundLength(shell.Diameter, shell.PropellantLength) / 1000);
                __instance.pickupTime = (float)state.Size.ReloadSeconds * .2f;
                __instance.totalDistance = (float)state.Size.ReloadSeconds * .2f;
                __instance.dropoffTime = (float)state.Size.ReloadSeconds * .4f;
                return;
            }
        }
        catch (Exception ex) { Plugin.ModLog.LogError($"[Carousel] Select native supply: {ex}"); }
    }
    // The native controller calls Update even with no crew contributors. Override only
    // tasks drawing a carousel round; fallback rack loading keeps the original crew speeds.
    [HarmonyPrefix, HarmonyPatch(typeof(LoadTask), nameof(LoadTask.Update))]
    private static void Speed(LoadTask __instance, ref float __0, ref float __1, ref float __2)
    {
        try
        {
            // Native Update skips UpdateRequest when no request exists. Bootstrap the first one without ordinary racks.
            if (__instance.State == LoadState.NeverLoaded && __instance.Request.TypeID.Value == ProjectileTypeID.Invalid.Value)
                SelectSupply(__instance);
            if (!Mechanical(__instance, out var state)) return;
            __0 = 1; __1 = 1; __2 = (float)state.Size.ReloadSeconds * .2f;
        }
        catch (Exception ex) { Plugin.ModLog.LogError($"[Carousel] Loading speeds: {ex}"); }
    }
    [HarmonyPostfix, HarmonyPatch(typeof(LoadTask), nameof(LoadTask.UpdateRequest))]
    private static void Timings(LoadTask __instance)
    {
        Guard("Loading phases", () =>
        {
            if (!Mechanical(__instance, out var state) || __instance.State != LoadState.Loading) return;
            var seconds = (float)state.Size.ReloadSeconds;
            __instance.pickupTime = seconds * .2f;
            __instance.totalDistance = seconds * .2f;
            __instance.dropoffTime = seconds * .4f;
        });
    }
    [HarmonyPostfix, HarmonyPatch(typeof(Cannon), nameof(Cannon.Build))]
    private static void CannonChanged(Cannon __instance)
    {
        Guard("Update basket capacity", () =>
        {
            foreach (var basket in Baskets(__instance))
            {
                var state = State(basket);
                if (state.Enabled && state.CannonId == __instance.VUID.Value)
                {
                    Mass(basket);
                    CarouselInspector.Redraw(basket);
                    var shell = __instance.shellBlueprintSlot?.Blueprint;
                    if (shell == null) continue;
                    var preview = $"cannon={__instance.VUID.Value}, shellBlueprint={__instance.ShellSizeBlueprintID}, caliber={shell.Diameter}, propellant={shell.PropellantLength}, fullRound={CarouselGeometry.FullRoundLength(shell.Diameter, shell.PropellantLength)}";
                    if (preview != state.LastPreview)
                    {
                        state.LastPreview = preview;
                        Plugin.ModLog.LogInfo($"[Carousel] Ammunition refreshed: {preview}");
                    }
                }
            }
        });
    }
    [HarmonyPostfix, HarmonyPatch(typeof(VehicleObjectSerialization), nameof(VehicleObjectSerialization.ToBlueprint))]
    private static void Save(VehicleObject __0, Sprocket.Vehicles.Serialization.VehicleObjectBlueprint __result)
    {
        // ToBlueprint inlines VehicleComponent.SaveData; its base-method detour is never reached on file saves.
        Guard("Save basket", () =>
        {
            var parts = __0.Components;
            for (int i = 0; i < Count(parts); i++)
            {
                var basket = parts[i].TryCast<TurretBasket>();
                if (basket == null) continue;
                var state = State(basket);
                CarouselPersistence.Write(__result.State, state.Enabled, state.CannonId, state.Layout);
                Plugin.ModLog.LogInfo($"[Carousel] Saved basket {basket.VUID.Value}: enabled={state.Enabled}, cannon={state.CannonId}, layout={state.Layout}");
            }
        });
    }
    [HarmonyPostfix, HarmonyPatch(typeof(VehicleComponent), nameof(VehicleComponent.LoadData))]
    private static void Load(VehicleComponent __instance, Il2CppSystem.Runtime.Serialization.SerializationInfo __0)
    {
        var basket = __instance.TryCast<TurretBasket>();
        if (basket == null) return;
        Guard("Load basket", () =>
        {
            var state = State(basket); Invalidate(state); ReleaseRegistration(state);
            var saved = CarouselPersistence.Read(__0);
            state.Enabled = saved.Enabled; state.CannonId = saved.CannonId; state.Layout = saved.Layout;
            if (saved.Enabled)
                Plugin.ModLog.LogInfo($"[Carousel] Restored basket {basket.VUID.Value}: cannon={state.CannonId}, layout={state.Layout}");
        });
    }
    [HarmonyPrefix, HarmonyPatch(typeof(VehicleComponent), nameof(VehicleComponent.EnablePhysicsInternal))]
    private static void Start(VehicleComponent __instance)
    {
        var basket = __instance.TryCast<TurretBasket>();
        if (basket != null) Invalidate(State(basket));
    }
    [HarmonyPrefix, HarmonyPatch(typeof(TurretBasket), nameof(TurretBasket.Release), new Type[0])]
    private static void Release(TurretBasket __instance)
    {
        CarouselInspector.Forget(__instance);
        if (States.Remove(__instance.Pointer, out var state)) { Invalidate(state); Guard("Release projectile registration", () => ReleaseRegistration(state)); }
    }
    // Use the game's resource setters; no Harmony detour of a native struct return.
    [HarmonyPostfix, HarmonyPatch(typeof(TurretBasket), nameof(TurretBasket.Build))]
    private static void Mass(TurretBasket __instance)
    {
        var basket = __instance;
        try
        {
            var state = State(basket);
            basket.SetMass(0, MassType.Mechanisms);
            basket.SetMass(0, MassType.Ammunition);
            if (!state.Enabled) return;
            var cannon = Cannons(basket).FirstOrDefault(c => c.VUID.Value == state.CannonId);
            if (cannon == null) return;
            var size = Calculate(basket, cannon);
            basket.SetMass((float)size.MechanismMassKg, MassType.Mechanisms);
            basket.SetMass(size.Capacity * cannon.ShellBlueprint.Mass, MassType.Ammunition);
        }
        catch (Exception ex) { Plugin.ModLog.LogError($"[Carousel] Basket mass: {ex}"); }
    }
}


