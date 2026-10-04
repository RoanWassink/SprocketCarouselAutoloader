using HarmonyLib;
using Sprocket.Vehicles;
using Sprocket.Vehicles.Cannons;
using Sprocket.Vehicles.Weapons;
using Sprocket.WeaponFramework;

namespace SprocketCarouselAutoloader;

internal static class AutoloaderCycle
{
    private static readonly HashSet<IntPtr> Substeps = new();
    internal static double Seconds(LoadTask task)
    {
        if (CarouselRuntime.Mechanical(task, out var carousel)) return carousel.Size.ReloadSeconds;
        var cannon = task.target?.TryCast<WeaponBehaviour>()?.mount?.TryCast<Cannon>();
        return BustleRuntime.Mechanical(task, cannon, out var bustle) ? BustleRuntime.Seconds(bustle, cannon!) : 0;
    }

    // Native Update advances only one phase per call and discards its overshoot.
    // Subdivide the SAME delta time for rapid automatic cycles; retain native stock use/state events.
    [HarmonyPrefix, HarmonyPriority(Priority.Last), HarmonyPatch(typeof(LoadTask), nameof(LoadTask.Update))]
    private static bool RapidCycle(LoadTask __instance, float __0, float __1, float __2, float __3)
    {
        if (Substeps.Contains(__instance.Pointer) || __3 <= 0 || !float.IsFinite(__3)) return true;
        var seconds = Seconds(__instance);
        if (seconds <= 0 || seconds >= .5) return true;
        Substeps.Add(__instance.Pointer);
        try { for (int i = 0; i < 8; i++) __instance.Update(__0, __1, __2, __3 / 8); }
        finally { Substeps.Remove(__instance.Pointer); }
        return false;
    }
}
