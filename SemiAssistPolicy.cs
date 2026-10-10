namespace SprocketCarouselAutoloader;

internal static class SemiAssistPolicy
{
    internal static double ArmScale(double caliberMm) => Math.Max(.12,caliberMm/125);
    internal static (double X,double Y,double Z) Outlet(double width,double length,double caliberMm,bool mirrored)
    {
        var scale=ArmScale(caliberMm);
        return ((mirrored?-1:1)*(Math.Max(.025,width)/2-.025*scale),.05*scale,Math.Max(.025,length)/2+.3475*scale);
    }
    internal readonly record struct Balance(bool Valid,double MassKg,double LengthM,double Weight,double Travel,double Handling)
    {
        internal double OverheadWork => Valid ? .75*Handling : 0;
    }
    internal static Balance Curve(double massKg,double lengthM)
    {
        if(!double.IsFinite(massKg) || massKg<=0 || !double.IsFinite(lengthM) || lengthM<=0)
            return new(false,massKg,lengthM,0,1,1);
        // Logistic evaluation in log space avoids both z/product and fourth-power overflow.
        var logZ=Math.Log(massKg)-Math.Log(35)+.35*Math.Log(lengthM);
        var small=Math.Exp(-4*Math.Abs(logZ));
        var weight=logZ>=0 ? 1/(1+small) : small/(1+small);
        return new(true,massKg,lengthM,weight,.95+1.30*weight,.90+3.10*weight);
    }
    internal static Balance Native(double projectileKg,double chargeKg,double fullLengthM)
    {
        if(!double.IsFinite(projectileKg) || projectileKg<=0 || !double.IsFinite(chargeKg) || chargeKg<0)
            return new(false,projectileKg+chargeKg,fullLengthM,0,1,1);
        return Curve(projectileKg+chargeKg,fullLengthM);
    }
    internal static float PickupWithOverhead(float nativePickup,Balance balance)
    {
        if(!balance.Valid || !float.IsFinite(nativePickup) || nativePickup<0) return nativePickup;
        var work=(double)nativePickup+balance.OverheadWork;
        return work<=float.MaxValue ? (float)work : nativePickup;
    }
    internal static float Rate(float native,double multiplier)
    {
        if(!float.IsFinite(native) || native<=0 || !double.IsFinite(multiplier) || multiplier<=0) return native;
        var boosted=(double)native*multiplier;
        return double.IsFinite(boosted) && boosted<=float.MaxValue ? (float)boosted : native;
    }
}
