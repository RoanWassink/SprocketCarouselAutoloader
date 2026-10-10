using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Sprocket.TurretBaskets;
using Sprocket.Vehicles;
using Sprocket.Vehicles.Turrets;
using Sprocket.Vehicles.AmmunitionStorage;
using Sprocket.Vehicles.Weapons;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SprocketCarouselAutoloader;

// Optional rendering only. Native geometry, collision, ammo and load tasks remain owned by Sprocket.
internal static class CarouselVisuals
{
    private sealed class Visual
    {
        internal readonly TurretBasket Basket;
        internal readonly BasketGenerator Generator;
        internal readonly GameObject Root;
        internal readonly List<Mesh> Meshes = new();
        internal readonly List<Material> Materials = new();
        internal readonly List<Renderer> Native = new();
        internal readonly List<(Mesh Mesh, int[] Triangles, int[] Prefix)> Ammo = new();
        internal AmmoRackBehaviour? Rack;
        internal Il2CppSystem.EventHandler? StorageChanged;
        internal int Displayed = -1, Capacity;
        internal Visual(TurretBasket basket, BasketGenerator generator, GameObject root)
        { Basket = basket; Generator = generator; Root = root; }
    }
    private static readonly Dictionary<IntPtr, Visual> Models = new();
    private static readonly Dictionary<IntPtr, Visual> Generators = new();

    private static void Guard(TurretBasket basket, Action work)
    {
        try { work(); }
        catch (Exception ex)
        {
            Remove(basket.Pointer);
            Plugin.ModLog.LogWarning($"[Carousel visual] Native basket restored after optional visual failure: {ex.Message}");
        }
    }

    [HarmonyPostfix, HarmonyPatch(typeof(TurretBasket), nameof(TurretBasket.Build))]
    private static void Rebuild(TurretBasket __instance) => Guard(__instance, () => Build(__instance));
    [HarmonyPostfix, HarmonyPatch(typeof(TurretBasket), nameof(TurretBasket.ApplyAssets))]
    private static void AssetsReady(TurretBasket __instance) => Guard(__instance, () => Build(__instance));

    private static void Build(TurretBasket basket)
    {
        Remove(basket.Pointer);
        var state = CarouselRuntime.State(basket);
        if (!state.Enabled) return;
        var generator = basket.generator?.TryCast<BasketGenerator>();
        if (generator?.transform == null || generator.SegmentCount == 0) return;
        var native = new List<Renderer>();
        for (int i = 0; i < generator.SegmentCount; i++)
        {
            var segment = generator.GetSegment(i);
            var floor = segment.FloorModel?.gameObject;
            var wall = segment.WallModel?.gameObject;
            foreach (var model in new[] { floor, wall })
                if (model != null)
                    foreach (var renderer in model.GetComponentsInChildren<Renderer>(true)) native.Add(renderer);
        }
        var original = native.Select(r => r.sharedMaterial).FirstOrDefault(m => m != null);
        if (original == null) return; // Assets can arrive asynchronously; ApplyAssets will retry.
        var cannon = CarouselRuntime.Cannons(basket).FirstOrDefault(c => c.VUID.Value == state.CannonId);
        var bounds = generator.LocalBounds;
        var scale = basket.Scale;
        if (Math.Abs(scale.x) < .0001f || Math.Abs(scale.y) < .0001f || Math.Abs(scale.z) < .0001f) return;
        CarouselSize size;
        double caliber;
        if (cannon != null)
        {
            size = CarouselRuntime.Calculate(basket, cannon);
            caliber = cannon.ShellBlueprint.Diameter / 1000.0;
        }
        else
        {
            var bp = basket.BlueprintSlot.Blueprint;
            var segments = bp.Segments.Select(s => new Segment(s.Diameter, s.Depth));
            var resolved = CarouselGeometry.ResolveBasket(segments, basket.ringDiameter);
            var diameter = resolved.Min(s => s.DiameterMm) * Math.Min(Math.Abs(scale.x), Math.Abs(scale.z));
            var depth = resolved.Sum(s => s.DepthMm) * Math.Abs(scale.y);
            size = new(0,0,diameter,depth,0,0,"", StoredProjectileMm:375, StoredChargeMm:408);
            caliber = .125;
        }
        if (size.DiameterMm <= 0 || size.DepthMm <= 0) return;
        var geometry = CarouselVisualGeometry.Build(size.DiameterMm / 1000, size.DepthMm / 1000,
            size.Capacity, caliber, Math.Max(.001,size.StoredProjectileMm/1000),
            Math.Max(0,size.StoredChargeMm/1000), state.Layout == CarouselLayout.VerticalCharge);
        var root = new GameObject("Carousel autoloader visual");
        root.transform.SetParent(generator.transform, false);
        root.transform.localPosition = new(bounds.center.x, bounds.min.y, bounds.center.z);
        root.transform.localScale = new(1/Math.Abs(scale.x),1/Math.Abs(scale.y),1/Math.Abs(scale.z));
        var visual = new Visual(basket, generator, root) { Capacity = size.Capacity };
        Models[basket.Pointer] = visual;
        Generators[generator.Pointer] = visual;
        visual.Native.AddRange(native);
        foreach (var data in geometry)
        {
            var child = new GameObject(data.Name);
            child.transform.SetParent(root.transform, false);
            var mesh = new Mesh { name = "Carousel " + data.Name };
            visual.Meshes.Add(mesh);
            var vertices = data.Vertices.Select(p => new Vector3((float)p.X,(float)p.Y,(float)p.Z)).ToArray();
            var normals = data.Normals.Select(p => new Vector3((float)p.X,(float)p.Y,(float)p.Z)).ToArray();
            mesh.vertices = new Il2CppStructArray<Vector3>(vertices);
            mesh.normals = new Il2CppStructArray<Vector3>(normals);
            mesh.triangles = new Il2CppStructArray<int>(data.Triangles.ToArray());
            mesh.RecalculateBounds();
            var material = new Material(original);
            visual.Materials.Add(material);
            var color = data.Material switch
            {
                "Projectile" => new Color(.40f,.43f,.33f,1),
                "Charge" => new Color(.49f,.38f,.22f,1),
                "DriveTrim" => new Color(.30f,.32f,.33f,1),
                "Cassette" => new Color(.19f,.23f,.18f,1),
                _ => new Color(.10f,.13f,.10f,1)
            };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor",color);
            if (material.HasProperty("_Color")) material.SetColor("_Color",color);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (data.SlotPrefixIndexCounts.Length > 0)
                visual.Ammo.Add((mesh,data.Triangles.ToArray(),data.SlotPrefixIndexCounts));
        }
        Sync(visual);
        Bind(visual,state.Rack);
    }

    private static void Sync(Visual visual)
    {
        if (visual.Root == null) return;
        visual.Root.SetActive(visual.Generator.visible);
        visual.Root.layer = visual.Generator.layer;
        foreach (var mesh in visual.Root.GetComponentsInChildren<Renderer>(true)) mesh.gameObject.layer = visual.Generator.layer;
        foreach (var renderer in visual.Native) if (renderer != null) renderer.enabled = false;
    }

    private static void Bind(Visual visual, AmmoRackBehaviour? rack)
    {
        if (visual.Rack?.Pointer != rack?.Pointer)
        {
            if (visual.Rack != null && visual.StorageChanged != null)
                visual.Rack.remove_StorageValueChanged(visual.StorageChanged);
            visual.Rack = rack;
            visual.Displayed = -1;
            visual.StorageChanged = null;
            if (rack != null)
            {
                visual.StorageChanged = DelegateSupport.ConvertDelegate<Il2CppSystem.EventHandler>(
                    (Action<Il2CppSystem.Object,Il2CppSystem.EventArgs>)((_,_) => Guard(visual.Basket, () => UpdateStock(visual))));
                rack.add_StorageValueChanged(visual.StorageChanged);
            }
        }
        UpdateStock(visual);
    }
    private static void UpdateStock(Visual visual)
    {
        var remaining=visual.Basket.HealthFraction<=0 ? 0 : visual.Rack==null ? visual.Capacity : Math.Clamp((int)visual.Rack.AmountStored,0,visual.Capacity);
        if(remaining==visual.Displayed) return;
        // Static model: show exactly the native rack stock, without indexing/transit state.
        foreach(var group in visual.Ammo)
            group.Mesh.triangles=new Il2CppStructArray<int>(group.Triangles.Take(group.Prefix[remaining]).ToArray());
        visual.Displayed=remaining;
    }
    [HarmonyPostfix, HarmonyPatch(typeof(LoadTask), nameof(LoadTask.UpdateRequest))]
    private static void SupplyReady(LoadTask __instance)
    {
        if (!CarouselRuntime.Mechanical(__instance,out var state)) return;
        if(!Models.TryGetValue(state.Basket.Pointer,out var visual)) return;
        Guard(state.Basket, () =>
        {
            Bind(visual,state.Rack);
        });
    }
    [HarmonyPostfix, HarmonyPatch(typeof(BasketGenerator), nameof(BasketGenerator.SetVisible))]
    private static void Visibility(BasketGenerator __instance)
    { if (Generators.TryGetValue(__instance.Pointer,out var visual)) Guard(visual.Basket, () => Sync(visual)); }
    [HarmonyPostfix, HarmonyPatch(typeof(BasketGenerator), nameof(BasketGenerator.ApplyLayer))]
    private static void Layer(BasketGenerator __instance)
    { if (Generators.TryGetValue(__instance.Pointer,out var visual)) Guard(visual.Basket, () => Sync(visual)); }
    [HarmonyPostfix, HarmonyPatch(typeof(TurretBasket), nameof(TurretBasket.OnFlagsChanged))]
    private static void Flags(TurretBasket __instance)
    { if (Models.TryGetValue(__instance.Pointer,out var visual)) Guard(__instance, () => Sync(visual)); }

    private static void Remove(IntPtr pointer)
    {
        if (!Models.Remove(pointer,out var visual)) return;
        Generators.Remove(visual.Generator.Pointer);
        if (visual.Rack != null && visual.StorageChanged != null)
        {
            try { visual.Rack.remove_StorageValueChanged(visual.StorageChanged); }
            catch (Exception ex) { Plugin.ModLog.LogWarning($"[Carousel visual] Stock event cleanup: {ex.Message}"); }
        }
        if (visual.Root != null) { visual.Root.SetActive(false); Object.Destroy(visual.Root); }
        foreach (var mesh in visual.Meshes) if (mesh != null) Object.Destroy(mesh);
        foreach (var material in visual.Materials) if (material != null) Object.Destroy(material);
        foreach (var renderer in visual.Native) if (renderer != null) renderer.enabled = visual.Generator.visible;
    }
    [HarmonyPrefix, HarmonyPatch(typeof(TurretBasket), nameof(TurretBasket.Release), new Type[]{})]
    private static void Release(TurretBasket __instance) => Remove(__instance.Pointer);
}


