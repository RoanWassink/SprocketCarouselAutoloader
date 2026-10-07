using HarmonyLib;
using Il2CppInterop.Runtime;
using Sprocket.UI;
using Sprocket.Vehicles.AmmunitionStorage;
using UnityEngine.Events;

namespace SprocketCarouselAutoloader;

internal static class BustleInspector
{
    private static bool open = true;
    private static Il2CppSystem.Action<bool> Bool(Action<bool> callback) => DelegateSupport.ConvertDelegate<Il2CppSystem.Action<bool>>(callback)!;
    [HarmonyPostfix, HarmonyPatch(typeof(AmmoRackEditor), nameof(AmmoRackEditor.OnGUI))]
    private static void Draw(AmmoRackEditor __instance, IGUILayout __0)
    {
        var rack = __instance.Component;
        if (!BustleRuntime.IsPart(rack)) return;
        CarouselRuntime.Guard("Bustle inspector", () =>
        {
            var state = BustleRuntime.State(rack);
            var ui = __0.TryCast<IGUIElementDrawer>();
            if (ui == null) return;
            __0.EndAllDropdowns();
            __0.BeginDropdown("Bustle autoloader (experimental)", open, Bool(v => open = v));
            try
            {
                void Changed() { rack.RequestRebuild(); __instance.RequestRedraw(); }
                ui.ToggleField("Automatic loader", state.Enabled, Bool(v => { state.Enabled = v; Changed(); }),
                    "Uses a finite ready magazine. An assigned crew loader replenishes it from normal racks after it empties.");
                ui.ToggleField("Mirror feed arm", state.MirrorFeedArm, Bool(v =>
                {
                    state.MirrorFeedArm = v;
                    BustleRuntime.InvalidateRuntimeCaches();
                    Changed();
                }), "Move the loading arm to the opposite side. The visible fork and feed reach move together; saved per autoloader.");
                var cannons = CarouselRuntime.Cannons(rack);
                var selected = cannons.FindIndex(c => c.VUID.Value == state.CannonId);
                var labels = new Il2CppSystem.Collections.Generic.List<string>();
                labels.Add("Select cannon...");
                foreach (var cannon in cannons) labels.Add($"{cannon.Blueprint.Name} | {cannon.Blueprint.Caliber} mm | ID {cannon.VUID.Value}");
                ui.Dropdown("Assigned cannon", labels.Cast<Il2CppSystem.Collections.Generic.IReadOnlyList<string>>(), selected + 1,
                    DelegateSupport.ConvertDelegate<UnityAction<int>>((Action<int>)(index =>
                    {
                        if (index < 0 || index > cannons.Count) return;
                        BustleRuntime.Assign(state, index == 0 ? null : cannons[index - 1]);
                        Changed();
                    }))!, "Cannons in the same turret. This rack follows the assigned cannon's ammunition.");
                ui.InfoField($"Ready magazine capacity: {rack.Capacity} complete shots", 2);
                if (selected < 0) { ui.InfoField("Place under the same turret and choose its cannon", 2); return; }
                var assigned = cannons[selected];
                var distance = BustleRuntime.Distance(state, assigned);
                ui.InfoField($"Feed arm to breech: {distance:0.00} / {BustleRuntime.MaximumDistance:0.00} m", 2);
                if (distance >= BustleRuntime.MaximumDistance) ui.InfoField("Outside native crew-loader reach: move the autoloader closer", 2);
                ui.InfoField($"Mechanical reload: {BustleRuntime.Seconds(state, assigned):0.00} s | " +
                    (BustleRuntime.Eligible(state, assigned) ? "ready magazine needs no crew loader" : "automatic feed unavailable"), 2);
                ui.InfoField($"Theoretical feed rate: {60 / BustleRuntime.Seconds(state, assigned):0} shots/min", 2);
                ui.InfoField($"Mechanism mass: {BustleTiming.MechanismMassKg(rack.Capacity):0} kg", 2);
                ui.InfoField("Crew refills from normal racks after this magazine empties. Each completed shot is ready immediately.", 2);
                if (state.Refilling) ui.InfoField($"Refill pending: next shot {state.RefillProgress:P0} | needs assigned loader and matching reserve", 2);
                ui.InfoField("Capacity and dimensions use the vanilla rack. Ammunition follows the cannon; do not select a different round above.", 2);
                if (rack.behaviour != null) ui.InfoField($"Last supply reading: {rack.behaviour.AmountStored}/{rack.behaviour.Capacity}", 2);
                if (!BustleRuntime.Eligible(state, assigned)) ui.InfoField("Disabled, damaged, empty design or ammunition not synchronized; manual loading rules apply", 2);
                ui.InfoField("Native rack damage is retained. No belt, rammer animation, blast door or blow-out panels yet.", 2);
            }
            finally { __0.EndAllDropdowns(); }
        });
    }
}
