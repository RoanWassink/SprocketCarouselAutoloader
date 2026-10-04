namespace SprocketCarouselAutoloader;

internal sealed class OncePerFrame
{
    private int lastFrame = -1;
    internal bool Enter(int frame)
    {
        if (frame == lastFrame) return false;
        lastFrame = frame;
        return true;
    }
}

internal static class LoadWorkPolicy
{
    internal static int Steps(bool loading, bool waitingForCrew, double seconds, double deltaTime) =>
        loading && !waitingForCrew && double.IsFinite(seconds) && seconds > 0 && seconds < .5 &&
        double.IsFinite(deltaTime) && deltaTime > 0 ? 8 : 1;
}
