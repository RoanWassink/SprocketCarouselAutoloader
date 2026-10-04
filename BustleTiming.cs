namespace SprocketCarouselAutoloader;

public static class BustleTiming
{
    // Shared gameplay curve: compact rounds avoid the old fixed 4.5-second overhead.
    public static double ReloadSeconds(double caliberMm, double massKg, double lengthMm, double distanceMetres)
    {
        if (!double.IsFinite(caliberMm) || caliberMm <= 0 || !double.IsFinite(massKg) || !double.IsFinite(lengthMm) || !double.IsFinite(distanceMetres) ||
            massKg < 0 || lengthMm <= 0 || distanceMetres < 0)
            throw new ArgumentOutOfRangeException(nameof(distanceMetres), "Ammunition and path dimensions must be finite and valid.");
        var caliberScale = Math.Pow(caliberMm / 125, 2.4);
        // Preserve compact autocannon rates, smoothly shorten tank-calibre handling.
        var blend = Math.Clamp((caliberMm - 40) / 80, 0, 1);
        blend = blend * blend * (3 - 2 * blend);
        var coefficient = 8.71 + (6.18 - 8.71) * blend;
        var handling = coefficient * caliberScale * Math.Pow(lengthMm / 1341, .4) *
            (.9 + .1 * Math.Sqrt(massKg / 45.8));
        return .06 + handling;
    }
    public static double MechanismMassKg(int capacity) => 120 + 5 * Math.Max(0, capacity);
}
