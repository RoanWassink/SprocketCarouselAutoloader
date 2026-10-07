namespace SprocketCarouselAutoloader;

internal static class SettingsMigration
{
    internal static string? Resolve(ISet<string> keys, string canonical)
    {
        if (keys.Contains(canonical)) return canonical;
        if (!canonical.StartsWith("sprocket", StringComparison.Ordinal)) return null;
        var legacy = "roan" + canonical.Substring("sprocket".Length);
        return keys.Contains(legacy) ? legacy : null;
    }

    internal static void CopyLegacyConfig(string directory)
    {
        var canonical = Path.Combine(directory, "sprocket.carousel.cfg");
        var legacy = Path.Combine(directory, "nl.roan.sprocket.carousel.cfg");
        // Never replace an existing canonical configuration or modify the rollback file.
        if (File.Exists(canonical) || !File.Exists(legacy)) return;
        File.Copy(legacy, canonical, overwrite: false);
    }
}