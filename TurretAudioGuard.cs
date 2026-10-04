using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Sprocket.Vehicles;
using Sprocket.Vehicles.AudioSystems;
using Sprocket.Vehicles.AmmunitionStorage;
using Sprocket.Vehicles.Turrets;
using Sprocket.Vehicles.Weapons;
using UnityEngine;
using VehicleDesigner.FX;

namespace SprocketCarouselAutoloader;

internal static class TurretAudioGuard
{
    private static int Count<T>(Il2CppSystem.Collections.Generic.IReadOnlyList<T> list) =>
        list.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<T>>().Count;

    // Native Execute inlines the controller constructor and passes motor.behaviour
    // without checking null. Repair this optional audio stage only on autoloader vehicles.
    [HarmonyPrefix, HarmonyPatch(typeof(TurretAudioAssemblyStage), nameof(TurretAudioAssemblyStage.Execute))]
    private static bool Execute(TurretAudioAssemblyStage __instance)
    {
        var motors = new List<TraverseMotor>();
        bool ownsAutoloader = false;
        var objects = __instance.objects.Items;
        for (int i = 0; i < Count(objects); i++)
        {
            var parts = objects[i].Components;
            for (int j = 0; j < Count(parts); j++)
            {
                var component = parts[j];
                var rack = component.TryCast<AmmoRack>();
                if (rack != null && BustleRuntime.IsPart(rack)) ownsAutoloader = true;
                var basket = component.TryCast<TurretBasket>();
                if (basket != null && CarouselRuntime.State(basket).Enabled) ownsAutoloader = true;
                var motor = component.TryCast<TraverseMotor>();
                if (motor != null) motors.Add(motor);
            }
        }
        if (!ownsAutoloader || motors.All(m => m.behaviour != null)) return true;
        var sources = new List<AudioSource>();
        var controllers = new List<TurretAudioController>();
        try
        {
            foreach (var missing in motors.Where(m => m.behaviour == null))
                Plugin.ModLog.LogWarning($"[Autoloaders] Skipping missing turret audio behaviour: motor {missing.VUID.Value}, installed={missing.IsInstalled}, connected={missing.Connected}. Vehicle parts and crew roles retained.");
            if (__instance.modules.TryGetModule<AudioControllerVehicleModule>(out var module))
            {
                foreach (var motor in motors.Where(m => m.behaviour != null))
                {
                    var turret = motor.behaviour.TryCast<ITurretAudioSource>();
                    if (turret == null) continue;
                    var source = motor.gameObject.AddComponent<AudioSource>();
                    sources.Add(source);
                    source.playOnAwake = false;
                    source.clip = __instance.traverseClip;
                    source.spatialBlend = 1;
                    source.minDistance = __instance.minDistanceRolloff;
                    source.maxDistance = __instance.maxDistanceRolloff;
                    source.loop = true;
                    var controller = new TurretAudioController(turret, source)
                    { Volume = Mathf.Clamp01(__instance.volume), MinPriorityDistance = __instance.minPriorityDistance };
                    controllers.Add(controller);
                }
                foreach (var controller in controllers) module.Add(controller);
            }
            __instance.audioSources = new Il2CppReferenceArray<AudioSource>(sources.ToArray());
            Plugin.ModLog.LogInfo($"[Autoloaders] Turret audio assembly recovered: {controllers.Count} valid controllers; missing behaviours omitted.");
        }
        catch (Exception ex)
        {
            // Optional audio may fail; do not leave vehicle enablement half completed.
            __instance.audioSources = new Il2CppReferenceArray<AudioSource>(sources.ToArray());
            Plugin.ModLog.LogError($"[Autoloaders] Turret audio recovery failed; optional audio stage skipped: {ex}");
        }
        return false;
    }
}
