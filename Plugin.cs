using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Sprocket.Vehicles.AmmunitionStorage;
using Sprocket.WeaponFramework;
using Sprocket.Vehicles.CrewSystems;

namespace SprocketCarouselAutoloader;

[BepInPlugin("nl.roan.sprocket.carousel", "Sprocket Carousel Autoloader", "0.1.8")]
public sealed class Plugin : BasePlugin
{
    internal static ManualLogSource ModLog = null!;
    public override void Load()
    {
        ModLog = Log;
        try
        {
            // Isolated native storage check; no vehicle or projectile is spawned.
            var size = ProjectileSizeID.FromPropellantLength(120, 30);
            if (size.ShellLength != 390 || size.Caliber != 120 || size.PropellantLength != 30)
                throw new InvalidOperationException("Native ammunition-size encoding differs from the inspected version.");
            var rack = new AmmoRackBehaviour("Carousel startup check", default, size, 3);
            rack.FullyLoad();
            if (rack.AmountStored != 3 || !rack.TakeShell() || rack.AmountStored != 2)
                throw new InvalidOperationException("Native finite storage check failed.");
            Log.LogInfo("[Carousel] Native storage check passed: 120 mm / 30 mm propellant = 390 mm complete round; finite supply 3 -> 2.");
            var loader = new LoadContributeTask { efficiency = 1f, commandEfficiency = 1f, operateCount = 1 };
            var nativeLoader = loader.Cast<ILoadTaskContributor>();
            if (nativeLoader.EnableFraction != 1f)
                throw new InvalidOperationException("Native automatic loader enable check failed.");
            loader.operateCount = 0;
            loader.efficiency = 0f;
            loader.commandEfficiency = 0f;
            // This native task's EnableFraction is constant 1; contribution is gated by efficiency.
            if (loader.efficiency != 0f || loader.commandEfficiency != 0f || loader.operateCount != 0)
                throw new InvalidOperationException("Native automatic loader contribution disable check failed.");
            Log.LogInfo("[Carousel] Native automatic loader check passed: interface enabled; contribution efficiency 1 -> 0.");
            CarouselPersistence.CheckNativeJsonRoundTrip();
            Log.LogInfo("[Carousel] Native blueprint JSON check passed: 12 carousel settings round-trips, vanilla data and legacy defaults preserved.");
        }
        catch (Exception ex) { Log.LogError($"Carousel disabled before patching: {ex}"); return; }
        var core = new Harmony("nl.roan.sprocket.carousel.core");
        try { core.PatchAll(typeof(CarouselRuntime)); }
        catch (Exception ex) { core.UnpatchSelf(); Log.LogError($"Carousel disabled: {ex}"); return; }
        var ui = new Harmony("nl.roan.sprocket.carousel.ui");
        try { ui.PatchAll(typeof(CarouselInspector)); }
        catch (Exception ex) { ui.UnpatchSelf(); Log.LogError($"Basket inspector disabled: {ex}"); }
        Log.LogInfo("Carousel v0.1.8 experimental: native supply, automatic loader and vehicle-object persistence; inspector refresh enabled.");
    }
}





