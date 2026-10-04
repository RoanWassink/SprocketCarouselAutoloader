using HarmonyLib;
using System.Reflection;
using Sprocket.Vehicles;
using Sprocket.Vehicles.AmmunitionStorage;
using Sprocket.Vehicles.Cannons;
using Sprocket.Vehicles.Turrets;

namespace SprocketCarouselAutoloader;

// Topology changes during assembly/rebuild, not on every weapon update.
// Cache negative lookups too: ordinary cannons must not scan vehicles each frame.
internal sealed record LoadConnections(BustleState[] Bustles, AmmoRack[] Reserves,
    TurretBasket[] Baskets, CannonBreech? Breech);

[HarmonyPatch]
internal static class VehicleLoadIndex
{
    private static readonly Dictionary<IntPtr, LoadConnections> Connections = new();
    private static int Count<T>(Il2CppSystem.Collections.Generic.IReadOnlyList<T> list) =>
        list.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<T>>().Count;

    internal static LoadConnections Get(Cannon cannon)
    {
        if (Connections.TryGetValue(cannon.Pointer, out var found)) return found;
        var bustles = new List<BustleState>();
        var reserves = new List<AmmoRack>();
        var baskets = new List<TurretBasket>();
        CannonBreech? breech = null;
        var objects = cannon.Vehicle.ObjectReader.Items;
        int objectCount = Count(objects);
        for (int i = 0; i < objectCount; i++)
        {
            var parts = objects[i].Components;
            int partCount = Count(parts);
            for (int j = 0; j < partCount; j++)
            {
                var part = parts[j];
                var rack = part.TryCast<AmmoRack>();
                if (rack != null)
                {
                    if (!BustleRuntime.IsPart(rack)) reserves.Add(rack);
                    else if (CarouselRuntime.SameTurret(rack, cannon)) bustles.Add(BustleRuntime.State(rack));
                    continue;
                }
                var basket = part.TryCast<TurretBasket>();
                if (basket != null && CarouselRuntime.SameTurret(basket, cannon)) baskets.Add(basket);
                var candidate = part.TryCast<CannonBreech>();
                if (candidate?.parentCannonVuid.Value == cannon.VUID.Value) breech = candidate;
            }
        }
        bustles.Sort((a, b) => a.Rack.VUID.Value.CompareTo(b.Rack.VUID.Value));
        return Connections[cannon.Pointer] = new(bustles.ToArray(), reserves.ToArray(), baskets.ToArray(), breech);
    }

    internal static void Invalidate()
    {
        Connections.Clear();
        BustleRuntime.InvalidateRuntimeCaches();
    }

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> Targets()
    {
        yield return AccessTools.Method(typeof(AmmoRack), nameof(AmmoRack.Build));
        yield return AccessTools.Method(typeof(Cannon), nameof(Cannon.Build));
        yield return AccessTools.Method(typeof(TurretBasket), nameof(TurretBasket.Build));
        yield return AccessTools.Method(typeof(Cannon), nameof(Cannon.EnableBehaviour));
        yield return AccessTools.Method(typeof(Cannon), nameof(Cannon.DisableBehaviour));
        yield return AccessTools.Method(typeof(VehicleComponent), nameof(VehicleComponent.ReleaseInternal));
    }

    internal static void CheckHooks(string owner)
    {
        int count = 0;
        foreach (var method in Targets())
        {
            if (Harmony.GetPatchInfo(method)?.Prefixes.Any(p => p.owner == owner && p.PatchMethod.DeclaringType == typeof(VehicleLoadIndex)) != true)
                throw new InvalidOperationException($"Load index invalidation hook missing: {method.Name}");
            count++;
        }
        Plugin.ModLog.LogInfo($"[Autoloaders] Performance cache lifecycle checks passed: {count} rebuild/enable/disable/release hooks.");
    }

    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static void TopologyChanged() => Invalidate();
}
