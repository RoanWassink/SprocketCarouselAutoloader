using HarmonyLib;
using Sprocket.Vehicles;
using Sprocket.Vehicles.AmmunitionStorage;
using Sprocket.PartImporting;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SprocketCarouselAutoloader;

internal static class BustleVisuals
{
    private sealed record Visual(GameObject Root, Material Black, Material Steel);
    private static readonly Dictionary<IntPtr, Visual> Models = new();
    private static Sprite? icon, semiIcon;
    private static Texture2D? iconTexture, semiIconTexture;
    internal static Sprite Icon(bool assisted = false)
    {
        var cached=assisted ? semiIcon : icon;
        if(cached!=null) return cached;
        var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        var pixels = new Color[128 * 128];
        void Rect(int x, int y, int w, int h, Color color)
        { for (int j = y; j < y + h; j++) for (int i = x; i < x + w; i++) pixels[j * 128 + i] = color; }
        var black = new Color(.12f, .14f, .16f, 1);
        var edge = new Color(.48f, .53f, .58f, 1);
        Rect(12, 30, 81, 65, edge); Rect(16, 34, 73, 57, black);
        for (int row = 0; row < 3; row++) for (int col = 0; col < 5; col++)
        {
            Rect(22 + col * 13, 42 + row * 15, 8, 10, new Color(.83f, .65f, .25f, 1));
            Rect(24 + col * 13, 52 + row * 15, 4, 3, edge);
        }
        Rect(84, 60, 32, 9, edge); Rect(107, 38, 9, 31, edge);
        Rect(98, 33, 22, 7, new Color(.32f, .65f, .83f, 1));
        if(assisted)
        {
            void Circle(int cx,int cy,int radius,Color color)
            {
                for(int y=Math.Max(0,cy-radius);y<=Math.Min(127,cy+radius);y++)
                    for(int x=Math.Max(0,cx-radius);x<=Math.Min(127,cx+radius);x++)
                        if((x-cx)*(x-cx)+(y-cy)*(y-cy)<=radius*radius) pixels[y*128+x]=color;
            }
            var crew=new Color(.96f,.90f,.72f,1);
            Circle(102,103,23,edge); Circle(102,103,21,black);
            Circle(102,112,6,crew);
            Circle(102,94,10,crew); Rect(92,87,21,8,crew);
        }
        texture.SetPixels(pixels); texture.Apply();
        texture.name=assisted ? "Assisted autoloader crew icon" : "Bustle autoloader icon";
        texture.hideFlags=HideFlags.DontUnloadUnusedAsset;
        var created=Sprite.Create(texture,new Rect(0,0,128,128),new Vector2(.5f,.5f),128);
        created.name=texture.name; created.hideFlags=HideFlags.DontUnloadUnusedAsset;
        if(assisted) { semiIconTexture=texture;semiIcon=created; }
        else { iconTexture=texture;icon=created; }
        var previewFolder = System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "Mods", "CarouselAutoloader", "research");
        if (System.IO.Directory.Exists(previewFolder))
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(previewFolder, assisted ? "assisted-loader-icon.png" : "bustle-icon.png"), ImageConversion.EncodeToPNG(texture).ToArray());
        return created;
    }
    // The native icon getter shares its RVA with unrelated getters. Set the card field instead of detouring it.
    [HarmonyPostfix, HarmonyPatch(typeof(PartDefinitionCardFactory), nameof(PartDefinitionCardFactory.CreateCard))]
    private static void CardCreated(PartDisplayCard __result)
    {
        if(__result?.PartGuid==SemiAutoloader.PartGuid) __result.Icon=Icon(assisted:true);
        else if(__result?.PartGuid==BustleRuntime.PartGuid) __result.Icon=Icon();
    }

    private static void Box(Transform parent, Material material, string name, Vector3 position, Vector3 size)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name; box.layer = parent.gameObject.layer;
        var collider = box.GetComponent<Collider>(); collider.enabled = false; Object.Destroy(collider);
        box.transform.SetParent(parent, false); box.transform.localPosition = position; box.transform.localScale = size;
        box.GetComponent<MeshRenderer>().sharedMaterial = material;
    }
    private static Material Tint(Material original, Color color)
    {
        var material = new Material(original);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        return material;
    }
    [HarmonyPostfix, HarmonyPatch(typeof(AmmoRack), nameof(AmmoRack.Build))]
    private static void Rebuild(AmmoRack __instance)
    {
        if (!BustleRuntime.IsPart(__instance)) return;
        CarouselRuntime.Guard("Bustle visual rebuild", () =>
        {
            var nativeModel = __instance.model?.TryCast<AmmoRackModel>();
            if (nativeModel?.material == null) return;
            Remove(__instance.Pointer);
            var root = new GameObject("Bustle autoloader frame and rammer");
            root.layer = nativeModel.modelTransform != null ? nativeModel.modelTransform.gameObject.layer : __instance.gameObject.layer;
            root.transform.SetParent(__instance.transform, false);
            root.transform.localPosition = __instance.boundsCollider?.Centre ?? Vector3.zero;
            var black = Tint(nativeModel.material, new Color(.035f, .04f, .045f, 1));
            var steel = Tint(nativeModel.material, new Color(.22f, .25f, .28f, 1));
            Models[__instance.Pointer] = new(root, black, steel);
            var d = __instance.boundsSize;
            var sideSign = BustleRuntime.State(__instance).MirrorFeedArm ? -1f : 1f;
            var scale = (float)(BustleRuntime.State(__instance).Semi ? SemiAssistPolicy.ArmScale(__instance.projectileSizeID.Caliber) : BustleFeedGeometry.Scale(__instance.projectileSizeID.Caliber));
            float x = Mathf.Max(.025f, d.x), y = Mathf.Max(.025f, d.y), z = Mathf.Max(.025f, d.z), t = .035f * scale;
            Box(root.transform, black, "Base tray", new(0, -y / 2, 0), new(x + t * 2, t, z + t * 2));
            foreach (float side in new[] { -1f, 1f })
            {
                Box(root.transform, black, "Side rail", new(side * x / 2, 0, 0), new(t, y, z + t));
                Box(root.transform, black, "Top rail", new(0, y / 2, side * z / 2), new(x + t, t, t));
            }
            Box(root.transform, black, "Rammer support", new(sideSign * (x / 2 + .055f * scale), 0, 0), new(.07f * scale, y + .12f * scale, .09f * scale));
            Box(root.transform, steel, "Feed arm", new(sideSign * (x / 2 + .055f * scale), .05f * scale, z / 2 + .17f * scale), new(.045f * scale, .045f * scale, .34f * scale));
            var state = BustleRuntime.State(__instance);
            var outlet = state.Semi ? SemiAssistPolicy.Outlet(d.x,d.z,__instance.projectileSizeID.Caliber,state.MirrorFeedArm) :
                BustleFeedGeometry.Outlet(d.x, d.z, __instance.projectileSizeID.Caliber, state.MirrorFeedArm);
            Box(root.transform, black, "Loading fork", new((float)outlet.X, (float)outlet.Y, (float)outlet.Z - .0175f * scale), new(.20f * scale, .07f * scale, .035f * scale));
            root.SetActive(nativeModel.Visible);
        });
    }
    [HarmonyPostfix, HarmonyPatch(typeof(AmmoRackModel), nameof(AmmoRackModel.ApplyRendererSettings))]
    [HarmonyPatch(typeof(AmmoRackModel), nameof(AmmoRackModel.ApplyRenderers))]
    private static void MaterialReady(AmmoRackModel __instance)
    {
        CarouselRuntime.Guard("Bustle visual visibility", () =>
        {
            var parts = __instance.VehicleObject.Components;
            var count = parts.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<VehicleComponent>>().Count;
            for (int i = 0; i < count; i++)
            {
                var rack = parts[i].TryCast<AmmoRack>();
                if (rack == null || !BustleRuntime.IsPart(rack)) continue;
                if (!Models.TryGetValue(rack.Pointer, out var visual)) Rebuild(rack);
                else visual.Root.SetActive(__instance.Visible);
            }
        });
    }
    private static void Remove(IntPtr pointer)
    {
        if (!Models.Remove(pointer, out var visual)) return;
        visual.Root.SetActive(false); Object.Destroy(visual.Root); Object.Destroy(visual.Black); Object.Destroy(visual.Steel);
    }
    [HarmonyPrefix, HarmonyPatch(typeof(VehicleComponent), nameof(VehicleComponent.ReleaseInternal))]
    private static void Release(VehicleComponent __instance) => Remove(__instance.Pointer);
}
