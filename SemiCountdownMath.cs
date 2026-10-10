namespace SprocketCarouselAutoloader;
internal static class SemiCountdownMath
{
    internal readonly record struct Work(double Prepare,double Prepared,double Pickup,double Picked,double Travel,double Travelled,double Dropoff,double Dropped,double TravelRate,double HandlingRate);
    internal static bool TryTimes(Work w,out double remaining,out double total)
    {
        remaining=total=0;
        if(!double.IsFinite(w.Prepare) || w.Prepare<0 || !double.IsFinite(w.Prepared) || w.Prepared<0 ||
           !double.IsFinite(w.Pickup) || w.Pickup<0 || !double.IsFinite(w.Picked) || w.Picked<0 ||
           !double.IsFinite(w.Travel) || w.Travel<0 || !double.IsFinite(w.Travelled) || w.Travelled<0 ||
           !double.IsFinite(w.Dropoff) || w.Dropoff<0 || !double.IsFinite(w.Dropped) || w.Dropped<0 ||
           !double.IsFinite(w.TravelRate) || w.TravelRate<=0 || !double.IsFinite(w.HandlingRate) || w.HandlingRate<=0) return false;
        remaining=Math.Max(0,w.Prepare-w.Prepared)+(Math.Max(0,w.Pickup-w.Picked)+Math.Max(0,w.Dropoff-w.Dropped))/w.HandlingRate+Math.Max(0,w.Travel-w.Travelled)/w.TravelRate;
        total=w.Prepare+(w.Pickup+w.Dropoff)/w.HandlingRate+w.Travel/w.TravelRate;
        return double.IsFinite(remaining) && double.IsFinite(total) && remaining<=float.MaxValue && total<=float.MaxValue;
    }
    internal static double Fraction(double remaining,double total)=>total>0 ? Math.Clamp(1-remaining/total,0,1) : 0;
}
