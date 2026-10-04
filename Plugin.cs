using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Sprocket.Vehicles.AmmunitionStorage;
using Sprocket.WeaponFramework;
using Sprocket.Vehicles.CrewSystems;

namespace SprocketCarouselAutoloader;

[BepInPlugin("nl.roan.sprocket.carousel", "Sprocket Carousel Autoloader", "0.2.9")]
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
            var magazine = new AmmoRackBehaviour("Magazine refill startup check", default, size, 2);
            var total = rack.AmountStored + magazine.AmountStored;
            if (!rack.TakeShell()) throw new InvalidOperationException("Native reserve transfer failed.");
            magazine.LoadAmmo(1);
            if (magazine.AmountStored != 1 || rack.AmountStored + magazine.AmountStored != total)
                throw new InvalidOperationException("Native magazine transfer failed conservation check.");
            magazine.LoadAmmo(10);
            if (magazine.AmountStored != magazine.Capacity)
                throw new InvalidOperationException("Native magazine capacity clamp failed.");
            Log.LogInfo("[Bustle] Native refill check passed: one reserve shot transferred, total stock conserved, magazine capacity clamped.");
            var uncommandedPreparation = LoaderRules.GetPrepareTime(0f);
            var commandedPreparation = LoaderRules.GetPrepareTime(1f);
            if (!float.IsFinite(uncommandedPreparation) || uncommandedPreparation <= 0f || commandedPreparation != 0f)
                throw new InvalidOperationException("Native crew preparation-time check failed.");
            Log.LogInfo($"[Bustle] Native crew preparation check passed: command efficiency 0 adds {uncommandedPreparation:0.00} s; efficiency 1 adds no delay.");
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
            Log.LogInfo("[Carousel] Native blueprint JSON check passed: 12 carousel/bustle settings round-trips, vanilla data and legacy defaults preserved.");
        }
        catch (Exception ex) { Log.LogError($"Carousel disabled before patching: {ex}"); return; }
        var core = new Harmony("nl.roan.sprocket.carousel.core");
        try { core.PatchAll(typeof(CarouselRuntime)); core.PatchAll(typeof(BustleRuntime)); core.PatchAll(typeof(BustleClearance)); core.PatchAll(typeof(MagazineRefill)); core.PatchAll(typeof(AutoloaderCycle)); }
        catch (Exception ex) { core.UnpatchSelf(); Log.LogError($"Carousel disabled: {ex}"); return; }
        var ui = new Harmony("nl.roan.sprocket.carousel.ui");
        try { ui.PatchAll(typeof(CarouselInspector)); ui.PatchAll(typeof(BustleInspector)); }
        catch (Exception ex) { ui.UnpatchSelf(); Log.LogError($"Basket inspector disabled: {ex}"); }
        var visuals = new Harmony("nl.roan.sprocket.carousel.visuals");
        try { visuals.PatchAll(typeof(BustleVisuals)); BustleVisuals.Icon(); Log.LogInfo("[Bustle] Custom icon and optional frame/rammer visual hooks ready."); }
        catch (Exception ex) { visuals.UnpatchSelf(); Log.LogError($"Bustle visuals disabled: {ex}"); }
        var compatibility = new Harmony("nl.roan.sprocket.carousel.compatibility");
        try { compatibility.PatchAll(typeof(TurretAudioGuard)); }
        catch (Exception ex) { compatibility.UnpatchSelf(); Log.LogWarning($"Turret audio guard disabled: {ex}"); }
        var audio = new Harmony("nl.roan.sprocket.carousel.audio");
        try { AutoloaderAudio.PreloadClips(); audio.PatchAll(typeof(AutoloaderAudio)); Log.LogInfo("[Autoloaders] Custom reload audio enabled for cycles of at least one second."); }
        catch (Exception ex) { audio.UnpatchSelf(); Log.LogWarning($"Autoloader audio disabled: {ex}"); }
        Log.LogInfo($"[Bustle] Maximum feed distance uses native crew hand reach: {BustleRuntime.MaximumDistance:0.00} m.");
        Log.LogInfo("Autoloaders v0.2.9 experimental: carousel and bustle rack with optional custom icon/frame visuals.");
    }
}
