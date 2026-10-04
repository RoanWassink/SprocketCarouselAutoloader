using HarmonyLib;
using Il2CppInterop.Runtime;
using Sprocket.UI;
using Sprocket.Vehicles;
using Sprocket.Vehicles.Cannons;
using Sprocket.Vehicles.Turrets;
using UnityEngine.Events;
using VehicleDesigner.Compartments.Design;

namespace SprocketCarouselAutoloader;

internal static class CarouselInspector
{
    private static bool open = true;
    private static readonly Dictionary<IntPtr, TurretBasketEditor> Editors = new();
    internal static void Redraw(TurretBasket basket)
    {
        if (Editors.TryGetValue(basket.Pointer, out var editor)) editor.RequestRedraw();
    }
    internal static void Forget(TurretBasket basket) => Editors.Remove(basket.Pointer);
    private static Il2CppSystem.Action<bool> Bool(Action<bool> callback) => DelegateSupport.ConvertDelegate<Il2CppSystem.Action<bool>>(callback)!;
    private static UnityAction<int> Int(Action<int> callback) => DelegateSupport.ConvertDelegate<UnityAction<int>>(callback)!;
    private static Il2CppSystem.Collections.Generic.IReadOnlyList<string> Labels(IEnumerable<string> values)
    {
        var list = new Il2CppSystem.Collections.Generic.List<string>();
        foreach (var value in values) list.Add(value);
        return list.Cast<Il2CppSystem.Collections.Generic.IReadOnlyList<string>>();
    }
    [HarmonyPostfix, HarmonyPatch(typeof(TurretBasketEditor), nameof(TurretBasketEditor.OnGUI))]
    private static void Draw(TurretBasketEditor __instance, IGUILayout __0)
    {
        CarouselRuntime.Guard("Basket inspector", () =>
        {
            var basket = __instance.Component;
            Editors[basket.Pointer] = __instance;
            var state = CarouselRuntime.State(basket);
            var ui = __0.TryCast<IGUIElementDrawer>();
            if (ui == null) return;
            __0.EndAllDropdowns();
            __0.BeginDropdown("Carousel autoloader (experimental)", open, Bool(value => open = value));
            try
            {
                void Changed()
                {
                    CarouselRuntime.Invalidate(state);
                    basket.RequestRebuild();
                    __instance.RequestRedraw();
                }
                ui.ToggleField("Carousel autoloader", state.Enabled, Bool(value =>
                {
                    state.Enabled = value;
                    // Choose automatically only when there is a single unambiguous candidate.
                    var candidates = CarouselRuntime.Cannons(basket);
                    if (value && state.CannonId < 0 && candidates.Count == 1) state.CannonId = candidates[0].VUID.Value;
                    Changed();
                }), "Separate projectile and charge. Consumes finite ammunition; ordinary racks remain available for manual loading.");
                if (!state.Enabled) return;
                ui.Dropdown("Storage layout", Labels(new[] { "T-72 style: both horizontal", "T-64/80 style: upright charge" }), (int)state.Layout,
                    Int(index => { if (index is < 0 or > 1) return; state.Layout = (CarouselLayout)index; Changed(); }),
                    "Projectiles point towards the centre. T-72 style stacks horizontal charges above them. T-64/80 style stores charges upright. Geometry is approximate.");
                var cannons = CarouselRuntime.Cannons(basket);
                var selected = cannons.FindIndex(c => c.VUID.Value == state.CannonId);
                var names = new[] { "Select cannon..." }.Concat(cannons.Select(c =>
                    $"{c.Blueprint.Name} | {c.Blueprint.Caliber} mm | ID {c.VUID.Value}"));
                ui.Dropdown("Assigned cannon", Labels(names), selected + 1, Int(index =>
                {
                    if (index < 0 || index > cannons.Count) return;
                    state.CannonId = index == 0 ? -1 : cannons[index - 1].VUID.Value;
                    // Prevent two baskets feeding the same cannon.
                    if (index > 0)
                    {
                        foreach (var bustle in BustleRuntime.Racks(cannons[index - 1]))
                            if (bustle.CannonId == state.CannonId)
                            { bustle.CannonId = -1; bustle.AutoAssign = false; bustle.Rack.RequestRebuild(); }
                        foreach (var other in CarouselRuntime.Baskets(cannons[index - 1]))
                        {
                            var otherState = CarouselRuntime.State(other);
                            if (other.Pointer != basket.Pointer && otherState.CannonId == state.CannonId)
                            { otherState.CannonId = -1; CarouselRuntime.Invalidate(otherState); other.RequestRebuild(); }
                        }
                    }
                    Changed();
                }), "Only installed cannons in this turret are listed. Each carousel serves one cannon.");
                ui.InfoField("Ammunition follows assigned cannon; choose its profile on the cannon", 2);
                if (selected < 0)
                {
                    ui.InfoField(cannons.Count == 0 ? "No installed cannon found in this turret" : "Select a cannon to calculate capacity", 2);
                    return;
                }
                var cannon = cannons[selected];
                var size = CarouselRuntime.Calculate(basket, cannon);
                var shell = cannon.shellBlueprintSlot?.Blueprint;
                if (shell == null) { ui.InfoField("Ammunition blueprint is not ready", 2); return; }
                var roundLength = CarouselGeometry.FullRoundLength(shell.Diameter, shell.PropellantLength);
                ui.InfoField(state.Layout == CarouselLayout.VerticalCharge
                    ? $"Capacity: {size.Capacity} complete shots | radial footprint: {size.StoredProjectileMm:0} mm"
                    : $"Capacity: {size.Capacity} complete shots | radial length limit: {size.MaxLengthMm:0} mm", 2);
                ui.InfoField($"Storage allocation: projectile {size.StoredProjectileMm:0} mm | charge {size.StoredChargeMm:0} mm", 2);
                ui.InfoField($"Maximum cannon propellant setting: {Math.Floor(size.MaxChargeLengthMm):0} mm (storage constraints)", 2);
                ui.InfoField($"Required basket diameter: {size.RequiredDiameterMm:0} mm | current: {size.DiameterMm:0} mm", 2);
                ui.InfoField($"Required basket depth: {Math.Ceiling(size.RequiredDepthMm):0} mm | current: {size.DepthMm:0} mm", 2);
                ui.InfoField("Usable diameter = smallest of turret ring and segments; depth = sum of all segments", 2);
                ui.InfoField($"Native length: {roundLength} mm | propellant setting: {shell.PropellantLength} mm | {shell.Mass:0.0} kg per shot", 2);
                ui.InfoField(state.Layout == CarouselLayout.VerticalCharge
                    ? $"Fixed projectile body; excess propellant requires height. Available radial space: {size.MaxLengthMm:0} mm."
                    : "Storage uses a scaled 408 mm charge cap; remaining length goes to the projectile. Ballistics unchanged.", 2);
                var tooltip = new UITooltip { Header = "Refresh carousel", Body = "Read the assigned cannon's current ammunition dimensions again." };
                ui.Button("Refresh ammunition information", DelegateSupport.ConvertDelegate<UnityAction>((Action)(() =>
                { basket.RequestRebuild(); __instance.RequestRedraw(); }))!, ref tooltip);
                ui.InfoField($"Mechanical reload: {size.ReloadSeconds:0.00} s | no loader required for carousel rounds", 2);
                ui.InfoField($"Added full mass: {size.MechanismMassKg + size.Capacity * cannon.ShellBlueprint.Mass:0} kg", 2);
                if (size.Reason.Length > 0) ui.InfoField(size.Reason, 2);
                if (state.Rack != null)
                    ui.InfoField($"Last test supply: {state.Rack.AmountStored}/{state.Rack.Capacity} shots remaining", 2);
                else if (size.Capacity > 0)
                    ui.InfoField($"Design capacity: {size.Capacity} shots; own virtual rack created in Play (normal racks optional)", 2);
                else ui.InfoField("Carousel cannot supply this ammunition; normal racks are used", 2);
                ui.InfoField("When depleted: manual loading from normal racks. Basket damage/cook-off and animation are not implemented yet.", 2);
            }
            finally { __0.EndAllDropdowns(); }
        });
    }
}
