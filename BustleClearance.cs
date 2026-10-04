using HarmonyLib;
using Sprocket.Vehicles;
using Sprocket.Vehicles.AmmunitionStorage;
using Sprocket.Vehicles.Cannons;
using Sprocket.Vehicles.Colliders;

namespace SprocketCarouselAutoloader;

internal static class BustleClearance
{
    // Native CheckObstructions calls this unique method before recording either side.
    // RequiredSpace only: selection, damage and physical collision geometry remain intact.
    [HarmonyPostfix, HarmonyPatch(typeof(VehicleColliderRegister), nameof(VehicleColliderRegister.IsValidObstructionCandidate))]
    private static void Candidate(VehicleCollider __0, VehicleCollider __1, ref bool __result)
    {
        if (!__result) return;
        try
        {
            if (Exempt(__0, __1) || Exempt(__1, __0)) __result = false;
        }
        catch (Exception ex) { Plugin.ModLog.LogWarning($"[Bustle] Clearance check retained native collision: {ex.Message}"); }
    }

    private static bool Exempt(VehicleCollider rackCollider, VehicleCollider gunCollider)
    {
        if ((gunCollider.Type & ColliderType.RequiredSpace) == 0 ||
            (gunCollider.Layer != VehicleColliderLayer.WeaponLoadArea && gunCollider.Layer != VehicleColliderLayer.WeaponBreech)) return false;
        var rack = rackCollider.Owner?.TryCast<AmmoRack>() ?? rackCollider.AssociatedComponent?.TryCast<AmmoRack>();
        if (rack == null || !BustleRuntime.IsPart(rack)) return false;
        var state = BustleRuntime.State(rack);
        if (!state.Enabled || state.CannonId < 0) return false;
        var component = gunCollider.AssociatedComponent ?? gunCollider.Owner;
        var breech = component?.TryCast<CannonBreech>() ?? gunCollider.Owner?.TryCast<CannonBreech>();
        var cannon = component?.TryCast<Cannon>() ?? gunCollider.Owner?.TryCast<Cannon>();
        var id = breech?.parentCannonVuid.Value ?? cannon?.VUID.Value ?? -1;
        var assigned = CarouselRuntime.Cannons(rack).FirstOrDefault(c => c.VUID.Value == id);
        return id == state.CannonId && assigned != null && CarouselRuntime.SameTurret(rack, assigned);
    }
}

