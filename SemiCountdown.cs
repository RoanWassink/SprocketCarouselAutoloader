using HarmonyLib;
using Sprocket.Vehicles.Weapons;
using Sprocket.WeaponFramework;
namespace SprocketCarouselAutoloader;

// Optional display correction only: the native getter adds dropoff work without
// dividing by its actual manipulation speed. The fraction duplicates that numerator.
internal static class SemiCountdown
{
    private static bool Times(LoadTask t,out double remaining,out double total)
    {
        remaining=total=0;
        if(t.State!=LoadState.Loading || !SemiAutoloader.OwnsCycle(t)) return false;
        // These are already the EFFECTIVE rates stored by native Update; never boost again.
        return SemiCountdownMath.TryTimes(new(t.prepareTime,t.prepareCurrentTime,t.pickupTime,t.pickupCurrentTime,
            t.totalDistance,t.travelProgress,t.dropoffTime,t.dropoffCurrentTime,t.travelSpeed,t.manipulationSpeed),out remaining,out total);
    }
    [HarmonyPostfix,HarmonyPatch(typeof(LoadTask),nameof(LoadTask.RemainingTime),MethodType.Getter)]
    private static void Remaining(LoadTask __instance,ref float __result)
    {
        if(!Times(__instance,out var remaining,out _)) return; // Preserve native zero-rate sentinel.
        NativeCycleDiagnostics.Display(__instance,__result,remaining);
        __result=(float)remaining;
    }
    [HarmonyPostfix,HarmonyPatch(typeof(LoadTask),nameof(LoadTask.LoadFraction),MethodType.Getter)]
    private static void Fraction(LoadTask __instance,ref float __result)
    {
        if(Times(__instance,out var remaining,out var total) && total>0)
            __result=(float)SemiCountdownMath.Fraction(remaining,total);
    }
}
