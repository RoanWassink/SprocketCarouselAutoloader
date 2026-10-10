using HarmonyLib;
using Sprocket.Vehicles.Weapons;
using Sprocket.WeaponFramework;
namespace SprocketCarouselAutoloader;

// Session bounded native evidence. No inventory writes, frame strings or unlimited logs.
internal static class NativeCycleDiagnostics
{
    private static int budget=120;
    private sealed class Trace { internal int Last=-1, DisplayPhase=-1, Sources, Rates; internal bool Invalid; }
    private static readonly Dictionary<IntPtr,Trace> Tasks=new();
    private static Trace? Get(LoadTask task)
    {
        if(budget<=0) return null;
        if(Tasks.TryGetValue(task.Pointer,out var trace)) return trace;
        if(Tasks.Count>=12) return null;
        return Tasks[task.Pointer]=new();
    }
    private static void Emit(LoadTask t,string detail)
    {
        if(budget--<=0) return;
        Plugin.ModLog.LogInfo($"[Native cycle] task={t.Pointer.ToInt64():X} source={(t.rack?.Pointer.ToInt64() ?? 0):X} state={t.State} request={t.currentRequest} action={(int)t.action} pickup={t.pickupCurrentTime:0.000}/{t.pickupTime:0.000} travel={t.travelProgress:0.000}/{t.totalDistance:0.000} dropoff={t.dropoffCurrentTime:0.000}/{t.dropoffTime:0.000} {detail}");
    }
    internal static void Source(LoadTask t,string detail)
    { var d=Get(t); if(d!=null && d.Sources++<3) Emit(t,detail); }
    internal static void Rates(LoadTask t,float travel,float handling,double travelX,double handlingX)
    { var d=Get(t); if(d!=null && d.Rates++<3) Emit(t,$"real rates travel={travel:0.000} handling={handling:0.000}; travelX={travelX:0.000} handlingX={handlingX:0.000} prepare={t.prepareCurrentTime:0.000}/{t.prepareTime:0.000}"); }
    internal static void InvalidBalance(LoadTask t)
    {
        var d=Get(t);if(d==null || d.Invalid) return;d.Invalid=true;
        if(budget--<=0) return;
        Plugin.ModLog.LogWarning("[Native cycle] Invalid native full-cartridge mass/length: preserving native crew rates and work without balance overhead.");
    }
    internal static void Display(LoadTask t,float native,double corrected)
    {
        var d=Get(t);if(d==null || d.DisplayPhase==(int)t.action) return;
        d.DisplayPhase=(int)t.action;
        Emit(t,$"countdown native={native:0.000}s corrected={corrected:0.000}s effective travel={t.travelSpeed:0.000} handling={t.manipulationSpeed:0.000}");
    }
    [HarmonyPostfix,HarmonyPatch(typeof(LoadTask),nameof(LoadTask.Update))]
    private static void Observe(LoadTask __instance)
    {
        if(budget<=0 || __instance.rack==null) return;
        if(!Tasks.TryGetValue(__instance.Pointer,out var d)) return;
        var phase=10*(int)__instance.State+(int)__instance.action;
        if(d.Last==phase) return; d.Last=phase;
        Emit(__instance,"native phase changed");
    }
}



