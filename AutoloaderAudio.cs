using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Sprocket.Vehicles.Cannons;
using Sprocket.Vehicles.Weapons;
using Sprocket.WeaponFramework;
using UnityEngine;
using UnityEngine.Bindings;
using Object = UnityEngine.Object;

namespace SprocketCarouselAutoloader;

internal static class AutoloaderAudio
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSpan { internal IntPtr Begin; internal int Length; }
    private static readonly Dictionary<string, AudioClip> Clips = new();
    private sealed class Player
    {
        internal readonly AudioSource Source;
        internal bool Loading;
        internal Player(AudioSource source) => Source = source;
    }
    private static readonly Dictionary<IntPtr, Player> Players = new();
    private static readonly HashSet<IntPtr> FailedTasks = new();

    internal static void PreloadClips()
    {
        if (Marshal.SizeOf<NativeSpan>() != Marshal.SizeOf<ManagedSpanWrapper>() ||
            Marshal.OffsetOf<ManagedSpanWrapper>(nameof(ManagedSpanWrapper.begin)).ToInt32() != 0 ||
            Marshal.OffsetOf<ManagedSpanWrapper>(nameof(ManagedSpanWrapper.length)).ToInt32() != IntPtr.Size)
            throw new InvalidOperationException("Unity audio span layout differs from the inspected bindings.");
        foreach (var name in new[] { "t90", "t64", "bustle" })
        {
            if (Clips.TryGetValue(name, out var existing) && existing != null) continue;
            using var stream = typeof(AutoloaderAudio).Assembly.GetManifestResourceStream($"Autoloaders.Audio.{name}.wav")
                ?? throw new FileNotFoundException($"Embedded reload sound {name} missing.");
            var wave = PcmWave.Read(stream, preserveStereo: name != "bustle");
            var clip = AudioClip.Construct_Internal();
            // Managed references do not keep Unity assets alive across scene cleanup.
            clip.hideFlags = HideFlags.HideAndDontSave;
            var title = $"Autoloader {name}".ToCharArray();
            var titlePin = GCHandle.Alloc(title, GCHandleType.Pinned);
            var samplePin = GCHandle.Alloc(wave.Samples, GCHandleType.Pinned);
            try
            {
                // Inspected Unity 6000.3 wrappers take a blittable {pointer, length}
                // span and a native Unity object pointer. Avoid the broken span helpers.
                var nativeLabel = new NativeSpan { Begin = titlePin.AddrOfPinnedObject(), Length = title.Length };
                ref var label = ref Unsafe.As<NativeSpan, ManagedSpanWrapper>(ref nativeLabel);
                var frames = wave.Samples.Length / wave.Channels;
                AudioClip.CreateUserSound_Injected(clip.m_CachedPtr, ref label, frames, wave.Channels, wave.Frequency, false);
                var nativeData = new NativeSpan { Begin = samplePin.AddrOfPinnedObject(), Length = wave.Samples.Length };
                ref var data = ref Unsafe.As<NativeSpan, ManagedSpanWrapper>(ref nativeData);
                if (!AudioClip.SetData_Injected(clip.m_CachedPtr, ref data, 0) ||
                    clip.samples != frames || clip.channels != wave.Channels || clip.frequency != wave.Frequency)
                    throw new InvalidOperationException($"Native audio upload failed for {name}.");
                Clips[name] = clip;
                Plugin.ModLog.LogInfo($"[Autoloaders] Custom {name} audio uploaded: {clip.samples} samples, {clip.channels} channels, {clip.frequency} Hz, {clip.length:0.00} s.");
            }
            catch { Object.Destroy(clip); throw; }
            finally { samplePin.Free(); titlePin.Free(); }
        }
    }

    // Native UpdateRequest inlines some transitions, bypassing SetState hooks.
    // Observe the task too so every mechanical cycle gets a cue.
    [HarmonyPostfix, HarmonyPatch(typeof(LoadTask), nameof(LoadTask.Update))]
    private static void Updated(LoadTask __instance) => Observe(__instance);

    [HarmonyPostfix, HarmonyPatch(typeof(LoadTask), nameof(LoadTask.SetState))]
    private static void StateChanged(LoadTask __instance) => Observe(__instance);

    private static void Observe(LoadTask task)
    {
        GuardAudio(task, () =>
        {
            var cannon = task.target?.TryCast<WeaponBehaviour>()?.mount?.TryCast<Cannon>();
            if (cannon == null) return;
            var seconds = AutoloaderCycle.Seconds(task);
            var active = task.State == LoadState.Loading && !MagazineRefill.Waiting(task) && seconds >= 1;
            Players.TryGetValue(cannon.Pointer, out var player);
            if (!active)
            {
                if (player != null) { player.Loading = false; if (player.Source != null) player.Source.Stop(); }
                return;
            }
            if (player?.Loading == true) return;
            var name = CarouselRuntime.Mechanical(task, out var carousel)
                ? (carousel.Layout == CarouselLayout.VerticalCharge ? "t64" : "t90") : "bustle";
            if (!Clips.TryGetValue(name, out var clip) || clip == null)
            {
                PreloadClips();
                if (!Clips.TryGetValue(name, out clip) || clip == null) return;
            }
            if (player == null || player.Source == null)
            {
                player = new(cannon.gameObject.AddComponent<AudioSource>());
                Players[cannon.Pointer] = player;
            }
            var source = player.Source;
            source.playOnAwake = false;
            source.clip = clip;
            source.loop = false;
            // Natural speed when the sound fits; longer clips fit the cycle up to
            // 2x speed, stopping on completion instead of looping.
            source.pitch = (float)Math.Clamp(clip.length / seconds, 1, 2);
            source.spatialBlend = 1;
            source.minDistance = 1;
            source.maxDistance = 20;
            source.volume = .6f;
            source.dopplerLevel = 0;
            player.Loading = true;
            source.Play();
            Plugin.ModLog.LogDebug($"[Autoloaders] {name} reload audio started for cannon {cannon.VUID.Value}.");
        });
    }

    private static void GuardAudio(LoadTask task, Action action)
    {
        if (FailedTasks.Contains(task.Pointer)) return;
        try { action(); }
        catch (Exception ex)
        {
            FailedTasks.Add(task.Pointer);
            Plugin.ModLog.LogWarning($"[Autoloaders] Reload audio disabled for this task until next Play: {ex.Message}");
        }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(Cannon), nameof(Cannon.DisableBehaviour))]
    private static void Release(Cannon __instance)
    {
        var task = __instance.Behaviour?.LoadTask?.TryCast<LoadTask>();
        if (task != null) FailedTasks.Remove(task.Pointer);
        if (!Players.Remove(__instance.Pointer, out var player) || player.Source == null) return;
        player.Source.Stop();
        Object.Destroy(player.Source);
    }
}
