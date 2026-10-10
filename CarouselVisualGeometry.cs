using System;
using System.Collections.Generic;
using System.Linq;

namespace SprocketCarouselAutoloader;

// Original decorative geometry. No Unity objects, colliders, ammunition or capacity logic.
public readonly record struct CarouselVisualVector(double X, double Y, double Z)
{
    public static CarouselVisualVector operator +(CarouselVisualVector a, CarouselVisualVector b) => new(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
    public static CarouselVisualVector operator -(CarouselVisualVector a, CarouselVisualVector b) => new(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
    public static CarouselVisualVector operator *(CarouselVisualVector a, double b) => new(a.X*b,a.Y*b,a.Z*b);
    public double Length => Math.Sqrt(X*X+Y*Y+Z*Z);
    public CarouselVisualVector Unit => this*(1/Length);
    public static CarouselVisualVector Cross(CarouselVisualVector a, CarouselVisualVector b) => new(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
}

public sealed class CarouselVisualMesh
{
    public string Name { get; }
    public string Material { get; }
    // -1 means mechanism; nonnegative numbers belong to a native capacity slot.
    public int Slot { get; }
    public List<CarouselVisualVector> Vertices { get; } = new();
    public List<CarouselVisualVector> Normals { get; } = new();
    public List<int> Triangles { get; } = new();
    // Projectile/Charge meshes only: prefix index count for 0..slot count visible rounds.
    public int[] SlotPrefixIndexCounts { get; internal set; } = Array.Empty<int>();
    public int[] SlotTriangleEnds => SlotPrefixIndexCounts.Skip(1).ToArray();
    public CarouselVisualMesh(string name, string material, int slot=-1) { Name=name; Material=material; Slot=slot; }
    internal void Triangle(CarouselVisualVector a, CarouselVisualVector b, CarouselVisualVector c)
    {
        var n=CarouselVisualVector.Cross(b-a,c-a); if(n.Length<1e-10) return;
        n=n.Unit; var i=Vertices.Count; Vertices.AddRange(new[]{a,b,c}); Normals.AddRange(new[]{n,n,n}); Triangles.AddRange(new[]{i,i+1,i+2});
    }
    internal void Quad(CarouselVisualVector a, CarouselVisualVector b, CarouselVisualVector c, CarouselVisualVector d) { Triangle(a,b,c); Triangle(a,c,d); }
}

public static class CarouselVisualGeometry
{
    private static readonly CarouselVisualVector Up=new(0,1,0);
    public const int MaximumSlots=64;
    // All inputs in metres, except capacity. Origin is basket centre-bottom; Y up, Z forward.
    // Inputs must come from the accepted native storage allocation, never arbitrary shell estimates.
    public static IReadOnlyList<CarouselVisualMesh> Build(double diameter, double depth, int capacity,
        double calibre, double projectileLength, double chargeLength, bool verticalCharge, int visibleRounds=-1)
    {
        foreach(var x in new[]{diameter,depth,calibre,projectileLength})
            if(!double.IsFinite(x)||x<=0) throw new ArgumentOutOfRangeException("dimensions");
        if(!double.IsFinite(chargeLength)||chargeLength<0) throw new ArgumentOutOfRangeException(nameof(chargeLength));
        if(capacity<0||capacity>MaximumSlots) throw new ArgumentOutOfRangeException(nameof(capacity));
        if(visibleRounds<0) visibleRounds=capacity;
        visibleRounds=Math.Clamp(visibleRounds,0,capacity);
        var r=diameter/2; if(r<=.08||depth<.06) throw new ArgumentOutOfRangeException("basket");
        var output=new List<CarouselVisualMesh>();
        var frame=new CarouselVisualMesh("mechanism","Mechanism"); output.Add(frame);
        var hubRadius=Math.Min(r*.16,Math.Max(.09,r*.12));
        Ring(frame,hubRadius,r-.025,.015,Math.Min(.055,depth*.12),48);
        Ring(frame,r-.055,r-.025,.055,Math.Min(.13,depth*.3),48);
        Cylinder(frame,new(0,.055,0),new(0,Math.Min(.145,depth*.33),0),hubRadius,hubRadius*.86,16);
        var trim=new CarouselVisualMesh("drive_trim","DriveTrim"); output.Add(trim);
        Cylinder(trim,new(0,.055,0),new(0,.065,0),hubRadius*1.08,hubRadius*1.08,16);
        if(capacity==0) return Combine(output);
        var end=Math.Sqrt(Math.Max(0,(r-.04)*(r-.04)-Math.Pow((calibre*1.28+.012)/2,2)));
        if(projectileLength>end-hubRadius||(!verticalCharge&&chargeLength>end-hubRadius))
            throw new ArgumentOutOfRangeException("allocation","Native lengths must fit the supplied basket.");
        for(var slot=0;slot<capacity;slot++)
        {
            var angle=slot*2*Math.PI/capacity;
            var radial=new CarouselVisualVector(Math.Sin(angle),0,Math.Cos(angle));
            var tangent=new CarouselVisualVector(Math.Cos(angle),0,-Math.Sin(angle));
            var width=calibre*1.28+.012;
            var railLength=Math.Max(projectileLength,verticalCharge?projectileLength:chargeLength)+.012;
            var center=radial*(end-railLength/2);
            var cassette=new CarouselVisualMesh($"cassette_{slot:00}","Cassette"); output.Add(cassette);
            foreach(var side in new[]{-1,1})
                Box(cassette,center+tangent*(side*width*.48)+Up*.09,tangent,Up,radial,.012,.04,railLength);
            Box(cassette,radial*(end-.012)+Up*.095,tangent,Up,radial,width,.035,.018);
            if(slot>=visibleRounds) continue;
            var projectile=new CarouselVisualMesh($"projectile_{slot:00}","Projectile",slot); output.Add(projectile);
            var radius=calibre*.5;
            var py=.12+radius;
            var inner=radial*(end-projectileLength)+Up*py;
            var outer=radial*end+Up*py;
            var nose=Math.Min(projectileLength*.24,calibre*1.2);
            Cylinder(projectile,inner,inner+radial*nose,radius*.16,radius,8);
            Cylinder(projectile,inner+radial*nose,outer,radius,radius,8);
            var charge=new CarouselVisualMesh($"charge_{slot:00}","Charge",slot); output.Add(charge);
            if(chargeLength==0) continue;
            var cr=calibre*.62;
            if(verticalCharge)
            {
                var a=radial*(end-cr)+Up*(.12+calibre);
                Cylinder(charge,a,a+Up*chargeLength,cr,cr*.92,8);
            }
            else
            {
                var cy=.15+calibre+cr;
                Cylinder(charge,radial*(end-chargeLength)+Up*cy,radial*end+Up*cy,cr,cr*.95,8);
            }
        }
        // Native capacity must already have rejected baskets shallower than their ammunition.
        if(output.SelectMany(m=>m.Vertices).Any(p=>p.Y>depth+.001))
            throw new ArgumentOutOfRangeException("depth","Ammunition allocation exceeds the basket depth.");
        return Combine(output);
    }

    private static IReadOnlyList<CarouselVisualMesh> Combine(List<CarouselVisualMesh> source)
    {
        // Five combined meshes maximum: never one GameObject per shell.
        var result=new List<CarouselVisualMesh>();
        foreach(var group in source.GroupBy(m=>m.Material))
        {
            var mesh=new CarouselVisualMesh(group.Key.ToLowerInvariant(),group.Key);
            var prefix=new List<int>{0};
            foreach(var part in group)
            {
                var offset=mesh.Vertices.Count;
                mesh.Vertices.AddRange(part.Vertices);mesh.Normals.AddRange(part.Normals);
                mesh.Triangles.AddRange(part.Triangles.Select(i=>i+offset));
                if(part.Slot>=0) prefix.Add(mesh.Triangles.Count);
            }
            if(prefix.Count>1) mesh.SlotPrefixIndexCounts=prefix.ToArray();
            result.Add(mesh);
        }
        return result;
    }

    private static void Cylinder(CarouselVisualMesh m,CarouselVisualVector a,CarouselVisualVector b,double ra,double rb,int n)
    {
        var axis=(b-a).Unit;
        var u=CarouselVisualVector.Cross(Math.Abs(axis.Y)>.9?new(1,0,0):Up,axis).Unit;
        var v=CarouselVisualVector.Cross(axis,u);
        for(var i=0;i<n;i++)
        {
            var t=i*2*Math.PI/n; var q=(i+1)*2*Math.PI/n;
            var d=u*Math.Cos(t)+v*Math.Sin(t); var e=u*Math.Cos(q)+v*Math.Sin(q);
            m.Quad(a+d*ra,a+e*ra,b+e*rb,b+d*rb);
            m.Triangle(a,a+e*ra,a+d*ra); m.Triangle(b,b+d*rb,b+e*rb);
        }
    }
    private static void Ring(CarouselVisualMesh m,double inner,double outer,double bottom,double top,int n)
    {
        for(var i=0;i<n;i++)
        {
            var t=i*2*Math.PI/n; var q=(i+1)*2*Math.PI/n;
            CarouselVisualVector P(double radius,double y,double angle)=>new(Math.Sin(angle)*radius,y,Math.Cos(angle)*radius);
            var a=P(inner,bottom,t);var b=P(outer,bottom,t);var c=P(outer,bottom,q);var d=P(inner,bottom,q);
            var aa=P(inner,top,t);var bb=P(outer,top,t);var cc=P(outer,top,q);var dd=P(inner,top,q);
            m.Quad(aa,bb,cc,dd);m.Quad(a,d,c,b);m.Quad(b,c,cc,bb);m.Quad(a,aa,dd,d);
        }
    }
    private static void Box(CarouselVisualMesh m,CarouselVisualVector center,CarouselVisualVector x,CarouselVisualVector y,CarouselVisualVector z,double sx,double sy,double sz)
    {
        CarouselVisualVector P(int a,int b,int c)=>center+x*(a*sx/2)+y*(b*sy/2)+z*(c*sz/2);
        m.Quad(P(-1,-1,-1),P(-1,1,-1),P(1,1,-1),P(1,-1,-1));
        m.Quad(P(-1,-1,1),P(1,-1,1),P(1,1,1),P(-1,1,1));
        m.Quad(P(-1,-1,-1),P(1,-1,-1),P(1,-1,1),P(-1,-1,1));
        m.Quad(P(-1,1,-1),P(-1,1,1),P(1,1,1),P(1,1,-1));
        m.Quad(P(-1,-1,-1),P(-1,-1,1),P(-1,1,1),P(-1,1,-1));
        m.Quad(P(1,-1,-1),P(1,1,-1),P(1,1,1),P(1,-1,1));
    }
}
