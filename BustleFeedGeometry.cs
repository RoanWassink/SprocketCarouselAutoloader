namespace SprocketCarouselAutoloader;

public static class BustleFeedGeometry
{
    public static double Scale(double caliberMm) => Math.Clamp(caliberMm / 125, .12, 2);

    // Front of the visible loading fork, relative to the frame's bounds centre.
    public static (double X, double Y, double Z) Outlet(double width, double length, double caliberMm, bool mirrorFeedArm = false)
    {
        var scale = Scale(caliberMm);
        return ((mirrorFeedArm ? -1 : 1) * (Math.Max(.025, width) / 2 - .025 * scale), .05 * scale,
            Math.Max(.025, length) / 2 + (.33 + .035 / 2) * scale);
    }
}
