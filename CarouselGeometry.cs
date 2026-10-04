namespace SprocketCarouselAutoloader;

public readonly record struct Segment(double DiameterMm, double DepthMm);
public enum CarouselLayout { HorizontalCassette, VerticalCharge }
public readonly record struct CarouselSize(int Capacity, double MaxLengthMm, double DiameterMm,
    double DepthMm, double MechanismMassKg, double ReloadSeconds, string Reason,
    double RequiredDepthMm = 0, double MaxChargeLengthMm = 0, double RequiredDiameterMm = 0,
    double StoredProjectileMm = 0, double StoredChargeMm = 0);

public static class CarouselGeometry
{
    public static Segment[] ResolveBasket(IEnumerable<Segment> source, double ringDiameterMm)
    {
        var segments = source.ToArray();
        if (!double.IsFinite(ringDiameterMm) || ringDiameterMm <= 0) return Array.Empty<Segment>();
        return segments.Select((s, i) => new Segment(i == 0 && s.DiameterMm == 0 ? ringDiameterMm : Math.Min(s.DiameterMm, ringDiameterMm), s.DepthMm)).ToArray();
    }
    public static int FullRoundLength(int caliberMm, int propellantLengthMm) => checked(caliberMm * 3 + propellantLengthMm);
    // A calibre-scaled separate-charge allocation, not a replacement for native ballistics.
    public static (double Projectile, double Charge) StorageLengths(double caliber, double nativeLength,
        CarouselLayout layout = CarouselLayout.HorizontalCassette)
    {
        var charge = Math.Min(nativeLength - caliber * 3, caliber * (408.0 / 125));
        if (layout == CarouselLayout.VerticalCharge)
        {
            var body = caliber * (680.0 / 125);
            // A fixed radial body; budget above the reference shot becomes upright charge height.
            var overflow = Math.Max(0, nativeLength - body - caliber * (408.0 / 125));
            return (body, charge + overflow);
        }
        return (nativeLength - charge, charge);
    }
    private static double Width(double caliber) => caliber * 1.28 + 12;
    private static double Pitch(double caliber, CarouselLayout layout) => Width(caliber) *
        (layout == CarouselLayout.HorizontalCassette ? 1.40 : 1.12);
    private static double RadialEnd(double diameter, double caliber)
    {
        var outer = diameter / 2 - 40;
        var halfWidth = Width(caliber) / 2;
        return outer > halfWidth ? Math.Sqrt(outer * outer - halfWidth * halfWidth) : 0;
    }
    private static double RadialLimit(double diameter, double caliber, CarouselLayout layout)
    {
        var end = RadialEnd(diameter, caliber);
        // Storage reaches inward from the outer wall. Reserve the drive and at least three cassette positions.
        return Math.Max(0, Math.Min(end - Math.Max(180, diameter * .12),
            2 * (end - Pitch(caliber, layout) / Math.Sqrt(3))));
    }
    private static double RequiredDiameter(double caliber, double storedLength, CarouselLayout layout)
    {
        double low = 0, high = Math.Max(1000, storedLength * 4 + caliber * 4);
        while (RadialLimit(high, caliber, layout) < storedLength) high *= 2;
        for (int i = 0; i < 60; i++)
        {
            var mid = (low + high) / 2;
            if (RadialLimit(mid, caliber, layout) >= storedLength) high = mid;
            else low = mid;
        }
        return Math.Ceiling(high + .000001);
    }
    // Cassette pitch at the mean storage radius is calibrated, not a collision model for rectangular rounds.
    // Reference: 2.5 m diameter, 125 mm, 680 mm projectile + 408 mm charge => AZ 22, MZ 28.
    public static CarouselSize Calculate(IEnumerable<Segment> source, double caliberMm, double lengthMm, double shellMassKg,
        CarouselLayout layout = CarouselLayout.HorizontalCassette)
    {
        var segments = source.ToArray();
        if (segments.Length == 0 || segments.Any(s => !double.IsFinite(s.DiameterMm) ||
            !double.IsFinite(s.DepthMm) || s.DiameterMm <= 0 || s.DepthMm <= 0) ||
            !double.IsFinite(caliberMm) || !double.IsFinite(lengthMm) || !double.IsFinite(shellMassKg) ||
            caliberMm <= 0 || lengthMm < caliberMm * 3 || shellMassKg <= 0 || !Enum.IsDefined(typeof(CarouselLayout), layout))
            return new(0, 0, 0, 0, 0, 0, "Invalid basket or ammunition dimensions");
        var diameter = segments.Min(s => s.DiameterMm);
        var depth = segments.Sum(s => s.DepthMm);
        var (projectileLength, chargeLength) = StorageLengths(caliberMm, lengthMm, layout);
        var horizontal = layout == CarouselLayout.HorizontalCassette;
        var pitch = Pitch(caliberMm, layout);
        var end = RadialEnd(diameter, caliberMm);
        var maxLength = RadialLimit(diameter, caliberMm, layout);
        // Two horizontal thicknesses for AZ; projectile thickness + upright charge height for MZ.
        var requiredDepth = 150 + 18 + caliberMm + (horizontal ? caliberMm * 1.28 : Math.Max(chargeLength, caliberMm * 1.28));
        var chargeCap = caliberMm * (408.0 / 125);
        var verticalChargeLimit = Math.Max(0, depth - 150 - 18 - caliberMm);
        // Invert the storage allocation back to the cannon's native PropellantLength slider.
        var maxNativePropellant = horizontal
            ? (maxLength >= chargeCap ? maxLength - caliberMm * 3 + chargeCap : Math.Min(maxLength, chargeCap))
            : (verticalChargeLimit >= chargeCap ? projectileLength + verticalChargeLimit - caliberMm * 3 : verticalChargeLimit);
        var storedLength = horizontal ? Math.Max(projectileLength, chargeLength) : projectileLength;
        var centerRadius = end - storedLength / 2;
        int capacity = 0;
        string reason = "";
        if (projectileLength > maxLength) reason = "Storage projectile is too long for the basket diameter";
        else if (horizontal && chargeLength > maxLength) reason = "Storage charge is too long for the basket diameter";
        else if (depth < requiredDepth) reason = "Basket is too shallow for both components and mechanism";
        else if (centerRadius <= pitch / 2) reason = "Basket is too narrow for the drive and cassettes";
        else
        {
            capacity = Math.Min(64, (int)Math.Floor(Math.PI / Math.Asin(pitch / (2 * centerRadius))));
            if (capacity < 3) { capacity = 0; reason = "Fewer than three cassette positions fit"; }
        }
        var mass = 120 + diameter / 1000 * 40 + capacity * 5;
        var reload = BustleTiming.ReloadSeconds(caliberMm, shellMassKg, lengthMm, diameter / 1000 * .35);
        return new(capacity, maxLength, diameter, depth, mass, reload, reason, requiredDepth,
            Math.Max(0, maxNativePropellant), RequiredDiameter(caliberMm, storedLength, layout), projectileLength, chargeLength);
    }
}
