using HarmonyLib;
using Sprocket.Vehicles.Cannons;
using Sprocket.Vehicles.CrewSystems;
using Sprocket.Vehicles.Weapons;
using Sprocket.WeaponFramework;

namespace SprocketCarouselAutoloader;

internal static class SemiAutoloader
{
    internal static bool Available;
    private static readonly Dictionary<IntPtr,BustleState> Sources = new();
    private static readonly Dictionary<IntPtr,(IntPtr Rack,SemiAssistPolicy.Balance Balance)> Cycles=new();
    internal static void Track(BustleState state)
    {
        if(!state.Semi || state.Rack.behaviour==null) return;
        var pointer=state.Rack.behaviour.Pointer;
        if(Sources.TryGetValue(pointer,out var tracked) && ReferenceEquals(tracked,state)) return;
        Forget(state.Rack.Pointer); Sources[pointer]=state;
    }
    internal static void Forget(IntPtr rackPointer)
    {
        foreach(var key in Sources.Where(pair=>pair.Value.Rack.Pointer==rackPointer).Select(pair=>pair.Key).ToArray()) Sources.Remove(key);
        foreach(var key in Cycles.Where(pair=>pair.Value.Rack==rackPointer).Select(pair=>pair.Key).ToArray()) Cycles.Remove(key);
    }
    internal const string PartGuid = "f8db54a4-6f03-48c4-b163-990b2d3ec7c2";
    internal static bool HasCrew(Cannon cannon, bool working = false)
    {
        var breech = VehicleLoadIndex.Get(cannon).Breech;
        var operable = breech?.loaderOperable;
        var seat = operable?.AssignedOperator;
        if (breech == null || breech.HealthFraction <= 0 || operable == null || !operable.Valid ||
            seat?.ActiveBehaviour == null || !seat.ActiveBehaviour.Enabled || seat.ActiveBehaviour.HealthFraction <= 0) return false;
        if (!working) return true;
        var real = operable.behaviour?.TryCast<LoadContributeTask>();
        return real != null && real.operateCount > 0 && float.IsFinite(real.efficiency) && real.efficiency > 0;
    }
    // UpdateRequest's matching-source branch skips all native timing initialization.
    // Reconstruct only that skipped branch through the game's verified rules helpers.
    internal static void InitializeTimings(LoadTask task,BustleState state,Cannon cannon)
    {
        var shot=state.Rack.LoadInfo;
        var technique=LoaderRules.GetLoadTechnique(ref shot);
        var path=new Sprocket.Vehicles.VehicleComponentPath(state.Rack,cannon);
        path.aLocalPosition=state.Rack.LoadLocalPosition;
        path.bLocalPosition=cannon.LoadLocalPosition;
        path.Update();
        task.currentLoadInfo=shot;
        var balance=SemiAssistPolicy.Native(shot.ProjectileMass,shot.PropellantMass,shot.ShellLength);
        Cycles[task.Pointer]=(state.Rack.Pointer,balance);
        // Assignment from native base work, never +=: repeated initialization cannot stack overhead.
        task.pickupTime=SemiAssistPolicy.PickupWithOverhead(LoaderRules.CalculatePickupTime(ref shot,technique),balance);
        task.totalDistance=2*path.Distance*LoaderRules.GetTravelDistanceModifier(ref shot,technique);
        task.dropoffTime=LoaderRules.CalculateBreechRamTime(ref shot,technique);
        NativeCycleDiagnostics.Source(task,$"semi rack={state.Rack.VUID.Value} technique={technique} path={path.Distance:0.000} native full-cartridge M={balance.MassKg:0.000}kg L={balance.LengthM:0.000}m projectile={shot.ProjectileMass:0.000}kg charge={shot.PropellantMass:0.000}kg w={balance.Weight:0.0000} travelX={balance.Travel:0.000} handlingX={balance.Handling:0.000} overheadWork={balance.OverheadWork:0.000} valid={balance.Valid}");
        if(!balance.Valid) NativeCycleDiagnostics.InvalidBalance(task);
    }
    internal static bool OwnsCycle(LoadTask task)
    {
        return Available && task.rack!=null && Cycles.TryGetValue(task.Pointer,out var cycle) &&
            Sources.TryGetValue(task.rack.Pointer,out var state) && state.Semi && state.Enabled && cycle.Rack==state.Rack.Pointer;
    }
    [HarmonyPrefix, HarmonyPriority(Priority.Low), HarmonyPatch(typeof(LoadTask), nameof(LoadTask.Update))]
    private static bool Assist(LoadTask __instance, ref float __0, ref float __1)
    {
        if (__instance.State != LoadState.Loading || __instance.rack == null) return true;
        var cannon = __instance.target?.TryCast<WeaponBehaviour>()?.mount?.TryCast<Cannon>();
        if (cannon == null) return true;
        Sources.TryGetValue(__instance.rack.Pointer,out var state);
        if(state == null)
            foreach(var candidate in VehicleLoadIndex.Get(cannon).Bustles)
                if(candidate.Semi && candidate.Rack.behaviour?.Pointer == __instance.rack.Pointer)
                { state = candidate; Track(candidate); break; }
        if (state == null) return true;
        if (!BustleRuntime.Eligible(state,cannon) || !HasCrew(cannon,working:true))
        {
            // Never let an unrelated synthetic contributor operate the new crew-required magazine.
            return false; // Pause all native phases, including rate-independent preparation.
        }
        if(!Cycles.TryGetValue(__instance.Pointer,out var cycle) || cycle.Rack!=state.Rack.Pointer) return true;
        var balance=cycle.Balance;
        NativeCycleDiagnostics.Rates(__instance,__0,__1,balance.Travel,balance.Handling);
        __0 = SemiAssistPolicy.Rate(__0,balance.Travel);
        __1 = SemiAssistPolicy.Rate(__1,balance.Handling);
        // __2 is native commander preparation TIME, not a rammer rate: do not scale it.
        // Native work is retained with one pickup overhead; rates are scaled once.
        return true;
    }
}
