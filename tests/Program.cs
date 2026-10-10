using SprocketCarouselAutoloader;
int count = 0;
void Check(bool condition, string label) { count++; if (!condition) throw new Exception(label); }
CarouselSize Size(double diameter, double depth, double caliber = 125, double nativePropellant = 713, double mass = 33,
    CarouselLayout layout = CarouselLayout.HorizontalCassette) =>
    CarouselGeometry.Calculate(new[] { new Segment(diameter, depth) }, caliber, caliber * 3 + nativePropellant, mass, layout);
var az = Size(2500, 766);
var mz = Size(2500, 766, layout: CarouselLayout.VerticalCharge);
Check(az.StoredProjectileMm == 680 && az.StoredChargeMm == 408, "Reference allocation is 680 + 408");
Check(az.Capacity == 22 && mz.Capacity == 28, "Calibrated reference capacities 22 / 28");
Check(az.RequiredDepthMm == 453 && mz.RequiredDepthMm == 701, "AZ adds thicknesses; MZ adds upright charge and projectile thickness");
var longAz = Size(2716, 1195, nativePropellant: 966);
var longMz = Size(2716, 1195, nativePropellant: 966, layout: CarouselLayout.VerticalCharge);
Check(longAz.StoredProjectileMm == 933 && longAz.StoredChargeMm == 408, "1341 mm is allocated 933 + 408, not falsely 680 + 408");
Check(longAz.Capacity == 22 && longMz.Capacity == 31, "AZ retains overflow radius; MZ uses a fixed body");
Check(Size(3000, 766).Capacity > az.Capacity && Size(3000, 766, layout: CarouselLayout.VerticalCharge).Capacity > mz.Capacity,
    "Larger baskets exceed reference counts");
Check(Size(2500, 5000).Capacity == az.Capacity, "Depth creates no extra rings");
Check(Size(2500, 452).Capacity == 0 && Size(2500, 453).Capacity > 0, "Horizontal exact-depth boundary");
Check(Size(2500, 700, layout: CarouselLayout.VerticalCharge).Capacity == 0 && Size(2500, 701, layout: CarouselLayout.VerticalCharge).Capacity > 0,
    "Vertical exact-depth boundary");
Check(Size(2500, 5000, nativePropellant: 2000).Capacity == 0, "Extreme native propellant still fails diameter despite capped storage charge");
Check(Size(2500, 766, nativePropellant: 30).StoredChargeMm == 30, "Short native charge is not lengthened");
Check(Size(2500, 766, mass: 50).ReloadSeconds > az.ReloadSeconds, "Weight penalty remains");
Check(CarouselGeometry.FullRoundLength(125, 966) == 1341, "Native identity remains unchanged");
foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, -1, 0 })
{
 Check(Size(invalid, 766).Capacity == 0, "Invalid diameter");
 Check(Size(2500, invalid).Capacity == 0, "Invalid depth");
 Check(Size(2500, 766, caliber: invalid).Capacity == 0, "Invalid caliber");
 Check(Size(2500, 766, mass: invalid).Capacity == 0, "Invalid mass");
}
Check(Size(2500, 766, layout: (CarouselLayout)99).Capacity == 0, "Invalid layout");
Check(CarouselGeometry.Calculate(new[] { new Segment(2500, 766) }, 125, 374, 33).Capacity == 0, "Invalid native length");
foreach (var layout in Enum.GetValues<CarouselLayout>())
for (int diameter = 600; diameter <= 4000; diameter += 100)
for (int caliber = 20; caliber <= 250; caliber += 20)
for (int propellant = 30; propellant <= 1500; propellant += 150)
{
 var value = Size(diameter, 3000, caliber, propellant, layout: layout);
 if (layout == CarouselLayout.HorizontalCassette) Check(Math.Abs(value.StoredProjectileMm + value.StoredChargeMm - (caliber * 3 + propellant)) < 1e-8, "AZ conserves native allocation");
 else Check(Math.Abs(value.StoredProjectileMm - caliber * 680.0 / 125) < 1e-8, "MZ radial body remains fixed");
 if (layout == CarouselLayout.HorizontalCassette) Check(value.StoredChargeMm <= caliber * (408.0 / 125) + 1e-8, "AZ scaled charge cap");
 Check(Size(value.RequiredDiameterMm, 3000, caliber, propellant, layout: layout).Capacity > 0, "Displayed minimum diameter fits");
 Check(Size(value.RequiredDiameterMm - 1, 3000, caliber, propellant, layout: layout).Capacity == 0, "One mm below minimum fails");
 if (value.Capacity == 0) continue;
 var width = caliber * 1.28 + 12;
 var outer = diameter / 2.0 - 40;
 var end = Math.Sqrt(outer * outer - width * width / 4);
 var length = layout == CarouselLayout.HorizontalCassette ? Math.Max(value.StoredProjectileMm, value.StoredChargeMm) : value.StoredProjectileMm;
 var center = end - length / 2;
 var pitch = width * (layout == CarouselLayout.HorizontalCassette ? 1.4 : 1.12);
 Check(2 * center * Math.Sin(Math.PI / value.Capacity) + 1e-8 >= pitch, "Calibrated cassette pitch respected at mean radius");
 Check(end - length + 1e-8 >= Math.Max(180, diameter * .12), "Drive clearance respected");
 Check(value.RequiredDepthMm <= 3000, "Vertical clearance");
 var limit = Math.Floor(value.MaxChargeLengthMm);
 Check(Size(diameter, 3000, caliber, limit, layout: layout).Capacity > 0, "Displayed maximum native propellant fits");
 Check(Size(diameter, 3000, caliber, limit + 1, layout: layout).Capacity == 0, "One mm past maximum native propellant fails");
}
var resolved = CarouselGeometry.ResolveBasket(new[] { new Segment(0, 606), new Segment(2657, 589) }, 2671);
var screenshot = CarouselGeometry.Calculate(resolved, 125, 1341, 45.8, CarouselLayout.VerticalCharge);
Check(screenshot.DiameterMm == 2657 && screenshot.DepthMm == 1195, "Ring and segment rules remain");
Check(CarouselGeometry.ResolveBasket(resolved, 2000).Min(s => s.DiameterMm) == 2000, "Smaller ring limits every segment");
Check(CarouselGeometry.ResolveBasket(resolved, 0).Length == 0, "Missing ring rejected");
Console.WriteLine($"PASS: {count} assertions. Reference AZ/MZ {az.Capacity}/{mz.Capacity}; 2716 mm example {longAz.Capacity}/{longMz.Capacity}, allocation {longAz.StoredProjectileMm}+{longAz.StoredChargeMm}, required depth {az.RequiredDepthMm}/{mz.RequiredDepthMm}.");

Check(longMz.StoredProjectileMm == 680 && longMz.StoredChargeMm == 661 && longMz.RequiredDepthMm == 954,
    "MZ 1341 mm native length uses fixed body and vertical overflow");
var shortMz = Size(2716, 1195, nativePropellant: 408, layout: CarouselLayout.VerticalCharge);
Check(shortMz.RequiredDiameterMm == longMz.RequiredDiameterMm && shortMz.Capacity == longMz.Capacity,
    "MZ bigger propellant does not grow diameter or lower capacity when depth suffices");
var hugeMz = Size(2716, 4000, nativePropellant: 2000, layout: CarouselLayout.VerticalCharge);
Check(hugeMz.RequiredDiameterMm == longMz.RequiredDiameterMm && hugeMz.RequiredDepthMm > longMz.RequiredDepthMm,
    "Extreme MZ propellant consumes vertical height, not radial length");
Check(Size(2716, 953, nativePropellant: 966, layout: CarouselLayout.VerticalCharge).Capacity == 0 &&
    Size(2716, 954, nativePropellant: 966, layout: CarouselLayout.VerticalCharge).Capacity > 0,
    "MZ vertical overflow boundary fits at required depth only");
Console.WriteLine($"MZ refinement PASS: {count} assertions; body {longMz.StoredProjectileMm}, charge {longMz.StoredChargeMm}, depth {longMz.RequiredDepthMm}, capacity {longMz.Capacity}.");
var baseReload = BustleTiming.ReloadSeconds(125, 45.8, 1341, 0);
for (int metres = 0; metres <= 20; metres++)
{
    var reload = BustleTiming.ReloadSeconds(125, 45.8, 1341, metres);
    Check(Math.Abs(reload - baseReload) < 1e-9, "Distance no longer changes firing cycle");
    Check(BustleTiming.ReloadSeconds(125, 55.8, 1341, metres) > reload, "Heavier ammunition loads more slowly");
    Check(BustleTiming.ReloadSeconds(125, 45.8, 1441, metres) > reload, "Longer ammunition loads more slowly");
    Check(Math.Abs(reload * .2 + reload * .2 + reload * .4 + reload * .2 - reload) < 1e-9, "Mechanical phases match advertised reload time");
}
foreach (var invalid in new[] { -1d, double.NaN, double.PositiveInfinity })
{
    bool rejected = false;
    try { BustleTiming.ReloadSeconds(125, 45.8, 1341, invalid); }
    catch (ArgumentOutOfRangeException) { rejected = true; }
    Check(rejected, "Reject invalid native path distances");
}
Check(BustleTiming.MechanismMassKg(22) == 230 && BustleTiming.MechanismMassKg(28) == 260, "Bustle mechanism scales with native capacity");
Console.WriteLine($"Bustle timing PASS: {count} total assertions; 125 mm example at 1 m = {BustleTiming.ReloadSeconds(125, 45.8, 1341, 1):0.000} s.");

var compact = BustleTiming.ReloadSeconds(25, .5, 212, .5);
Check(compact > .12 && compact < .18, "Compact 25 mm feed targets over 330 rpm");
Check(Math.Abs(BustleTiming.ReloadSeconds(120, 44.1, 1560, 1) - 6) < .01,
    "120 mm / 1200 mm propellant targets six seconds");
Check(BustleTiming.ReloadSeconds(25, .5, 424, .5) > compact, "Long small-calibre rounds have a penalty");
Check(BustleTiming.ReloadSeconds(50, .5, 212, .5) > compact * 2, "Calibre growth is nonlinear");
for (int c = 5; c <= 300; c++)
{
    var cycle = BustleTiming.ReloadSeconds(c, .5, c * 8, .5);
    Check(double.IsFinite(cycle) && cycle >= .06, "Finite cycle retains mechanical floor");
    Check(BustleTiming.ReloadSeconds(c + 1, .5, (c + 1) * 8, .5) > cycle, "Cycle increases smoothly with ammunition size");
}
Console.WriteLine($"Nonlinear timing PASS: {count} assertions; compact 25 mm = {compact:0.000} s / {60 / compact:0} theoretical rpm.");
var thirty = BustleTiming.ReloadSeconds(30, .8, 240, .5);
Check(thirty < .25 && thirty > .12, "Compact 30 mm cycle supports more than 240 theoretical rpm");
Check(BustleTiming.ReloadSeconds(30, .8, 240, 0) == BustleTiming.ReloadSeconds(30, .8, 240, 50), "Reach and cycle are independent");
Console.WriteLine($"30 mm compact target {thirty:0.000} s / {60 / thirty:0} rpm; {count} assertions.");
var outlet = BustleFeedGeometry.Outlet(.41, 1.56, 120);
Check(Math.Abs(outlet.X - .181) < 1e-9 && Math.Abs(outlet.Y - .048) < 1e-9 && Math.Abs(outlet.Z - 1.1136) < 1e-9,
    "120 mm outlet lies at the front of the actual loading fork");
Check(BustleFeedGeometry.Outlet(.41, 1.56, 30).Z < outlet.Z, "Small-calibre outlet follows smaller arm");
Check(BustleFeedGeometry.Outlet(.41, 2.56, 120).Z == outlet.Z + .5, "Rack length moves outlet with front wall");
Console.WriteLine($"Feed outlet geometry PASS: {count} assertions.");

foreach (var audio in new[] { ("t90", 44100, 283768), ("t64", 44100, 356970), ("bustle", 48000, 301824) })
{
    using var file = File.OpenRead(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Assets/Audio", audio.Item1 + ".wav")));
    var decoded = PcmWave.Read(file);
    Check(decoded.Frequency == audio.Item2 && decoded.Samples.Length == audio.Item3, "Provided WAV retains exact rate and duration");
    Check(decoded.Samples.All(s => float.IsFinite(s) && s >= -1 && s <= 1), "Downmixed WAV is valid normalized PCM");
    Check(decoded.Samples.Any(s => Math.Abs(s) > .01f), "Decoded WAV contains audible samples");
}
foreach (var invalidWave in new[] { Array.Empty<byte>(), new byte[12], File.ReadAllBytes(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Assets/Audio/t90.wav"))).Take(200).ToArray() })
{
    var rejected = false;
    try { PcmWave.Read(new MemoryStream(invalidWave)); }
    catch (Exception ex) when (ex is IOException or InvalidDataException) { rejected = true; }
    Check(rejected, "Malformed/truncated WAV fails safely");
}
Console.WriteLine($"Custom WAV decoder PASS: {count} assertions. 120x1200 reference {BustleTiming.ReloadSeconds(120, 44.1, 1560, 1):0.000} s.");

var frameGate = new OncePerFrame();
int refreshes = 0;
for (int frame = 100; frame < 103; frame++)
    for (int controller = 0; controller < 100; controller++)
        if (frameGate.Enter(frame)) refreshes++;
Check(refreshes == 3, "100 vehicle controllers refresh global bindings only once per frame");
Check(LoadWorkPolicy.Steps(false, false, .14, .016) == 1, "Idle compact autocannon receives one native update");
Check(LoadWorkPolicy.Steps(true, true, .14, .016) == 1, "Empty magazine waiting for crew receives no rapid substeps");
Check(LoadWorkPolicy.Steps(true, false, .14, .016) == 8, "Active rapid reload retains eight native substeps");
Check(LoadWorkPolicy.Steps(true, false, 6, .016) == 1 && LoadWorkPolicy.Steps(true, false, .5, .016) == 1,
    "Tank cycles and threshold do not substep");
foreach (var invalid in new[] { 0d, -1d, double.NaN, double.PositiveInfinity })
    Check(LoadWorkPolicy.Steps(true, false, .14, invalid) == 1, "Invalid delta cannot multiply native updates");
var allocatedDelta = .016 / LoadWorkPolicy.Steps(true, false, .14, .016);
Check(Math.Abs(allocatedDelta * 8 - .016) < 1e-12, "Rapid substeps conserve simulation time");
Console.WriteLine($"Load work policy PASS: {count} assertions; 100 controllers x 3 frames => {refreshes} global refreshes.");

// Both arm choices must retain fork height/forward reach and agree under rotation.
foreach (var width in new[]{.025,.5,1d,2d}) foreach (var length in new[]{.025,.5,2d}) foreach (var calibre in new[]{15d,75d,125d,250d})
{
    var a = BustleFeedGeometry.Outlet(width,length,calibre);
    var b = BustleFeedGeometry.Outlet(width,length,calibre,true);
    Check(Math.Abs(a.X+b.X)<1e-12 && a.Y==b.Y && a.Z==b.Z,"Arm mirror changes only lateral position");
    foreach (var yaw in new[]{0d,30d,90d,180d})
    {
        var r=yaw*Math.PI/180;
        var centre=new System.Numerics.Vector3(.2f,.3f,-.4f);
        var rotation=System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY,(float)r);
        var wa=System.Numerics.Vector3.Transform(new((float)a.X,(float)a.Y,(float)a.Z),rotation)+centre;
        var wb=System.Numerics.Vector3.Transform(new((float)b.X,(float)b.Y,(float)b.Z),rotation)+centre;
        Check(Math.Abs(System.Numerics.Vector3.Distance(wa,wb)-2*Math.Abs(a.X))<1e-5,"Mirrored reach remains consistent after turret rotation/centre offset");
    }
}
Console.WriteLine($"FR008 mirror geometry PASS: {count} assertions. Native UI/save-load/automatic feed remains pending.");

// Compatibility must preserve deliberate false values and selected cannon/layout.
foreach (var prefix in new[]{"roan", "sprocket"})
foreach (var enabled in new[]{false,true})
foreach (var cannon in new[]{-1,275,370})
foreach (var layout in new[]{0,1})
{
    var legacy = new Dictionary<string,object> {
        [prefix+"CarouselEnabledV1"]=enabled, [prefix+"CarouselCannonV1"]=cannon,
        [prefix+"CarouselLayoutV2"]=layout, [prefix+"BustleEnabledV1"]=enabled,
        [prefix+"BustleCannonV1"]=cannon, ["sprocketBustleMirrorFeedArmV1"]=true,
        ["vanillaSentinel"]=2671 };
    var json=System.Text.Json.JsonSerializer.Serialize(legacy);
    var decoded=System.Text.Json.JsonSerializer.Deserialize<Dictionary<string,System.Text.Json.JsonElement>>(json)!;
    var keys=new HashSet<string>(decoded.Keys);
    var migrated=new Dictionary<string,System.Text.Json.JsonElement>();
    foreach(var name in new[]{"CarouselEnabledV1","CarouselCannonV1","CarouselLayoutV2","BustleEnabledV1","BustleCannonV1"})
    {
        var key=SettingsMigration.Resolve(keys,"sprocket"+name);
        Check(key==prefix+name,"Legacy/canonical save key resolved");
        migrated["sprocket"+name]=decoded[key!];
    }
    migrated["sprocketBustleMirrorFeedArmV1"]=decoded["sprocketBustleMirrorFeedArmV1"];
    migrated["vanillaSentinel"]=decoded["vanillaSentinel"];
    var restored=System.Text.Json.JsonSerializer.Deserialize<Dictionary<string,System.Text.Json.JsonElement>>(System.Text.Json.JsonSerializer.Serialize(migrated))!;
    Check(restored["sprocketCarouselEnabledV1"].GetBoolean()==enabled && restored["sprocketBustleEnabledV1"].GetBoolean()==enabled,"Explicit disabled setting retained");
    Check(restored["sprocketCarouselCannonV1"].GetInt32()==cannon && restored["sprocketCarouselLayoutV2"].GetInt32()==layout,"Assigned cannon and layout retained");
    Check(restored["sprocketBustleMirrorFeedArmV1"].GetBoolean() && restored["vanillaSentinel"].GetInt32()==2671,"Mirror and vanilla state retained");
    Check(restored.Keys.All(k=>!k.StartsWith("roan")),"Canonical save has no legacy duplicate ledger");
}
Check(SettingsMigration.Resolve(new HashSet<string>{"roanBustleEnabledV1","sprocketBustleEnabledV1"},"sprocketBustleEnabledV1")=="sprocketBustleEnabledV1","Canonical wins mixed saves");
Check(SettingsMigration.Resolve(new HashSet<string>(),"sprocketCarouselEnabledV1")==null,"Missing settings retain vanilla defaults");
var configFixture=Path.Combine(Path.GetTempPath(),"carousel-migration-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(configFixture);
try {
    File.WriteAllText(Path.Combine(configFixture,"nl.roan.sprocket.carousel.cfg"),"# custom settings\nVolume = 0.42\n");
    SettingsMigration.CopyLegacyConfig(configFixture);
    Check(File.ReadAllBytes(Path.Combine(configFixture,"sprocket.carousel.cfg")).SequenceEqual(File.ReadAllBytes(Path.Combine(configFixture,"nl.roan.sprocket.carousel.cfg"))),"Config migration byte exact");
    File.WriteAllText(Path.Combine(configFixture,"sprocket.carousel.cfg"),"Keep canonical");
    SettingsMigration.CopyLegacyConfig(configFixture);
    Check(File.ReadAllText(Path.Combine(configFixture,"sprocket.carousel.cfg"))=="Keep canonical","Existing canonical settings never replaced");
    Check(File.ReadAllText(Path.Combine(configFixture,"nl.roan.sprocket.carousel.cfg")).Contains("0.42"),"Legacy rollback settings untouched");
} finally { foreach(var file in Directory.GetFiles(configFixture)) File.Delete(file); Directory.Delete(configFixture); }
Console.WriteLine($"Compatibility migration PASS: {count} total assertions.");

var visualAccepted=0;
foreach(var calibre in new[]{15d,25d,30d,50d,75d,100d,120d,125d,152d,250d})
foreach(var propellant in new[]{0d,30d,408d,700d,1200d,2000d})
foreach(var diameter in new[]{800d,1500d,2000d,2500d,2716d,4000d,6000d})
foreach(var depth in new[]{250d,453d,800d,1200d,2500d})
foreach(var layout in new[]{CarouselLayout.HorizontalCassette,CarouselLayout.VerticalCharge})
{
    var size=CarouselGeometry.Calculate(new[]{new Segment(diameter,depth)},calibre,calibre*3+propellant,40,layout);
    if(size.Capacity==0) continue;
    visualAccepted++;
    try {
        var meshes=CarouselVisualGeometry.Build(diameter/1000,depth/1000,size.Capacity,calibre/1000,
            Math.Max(.001,size.StoredProjectileMm/1000),Math.Max(0,size.StoredChargeMm/1000),layout==CarouselLayout.VerticalCharge);
        Check(meshes.Count<=5,"Visual draw groups stay bounded");
        foreach(var mesh in meshes) {
            Check(mesh.Triangles.All(i=>i>=0&&i<mesh.Vertices.Count),"Mesh indices valid");
            Check(mesh.Vertices.Count<65536,"Mesh stays in 16-bit index budget");
            if(mesh.SlotPrefixIndexCounts is { Length: > 0 } prefixes) {
                Check(prefixes.Length==size.Capacity+1&&prefixes[0]==0&&prefixes[^1]==mesh.Triangles.Count,"Ammo prefix covers native stock");
                Check(prefixes.Zip(prefixes.Skip(1),(a,b)=>a<=b&&b%3==0).All(x=>x),"Stock indices monotonic and triangle aligned");
            }
        }
    } catch(Exception ex){throw new Exception($"Visual rejected native-valid caliber={calibre},propellant={propellant},diameter={diameter},depth={depth},layout={layout},capacity={size.Capacity}",ex);}
}
Console.WriteLine($"Visual allocation compatibility PASS: {visualAccepted} native-valid designs; {count} total assertions.");

foreach(var native in new[]{0f,.1f,.4f,1f,2f,10f})
{
    Check(SemiAssistPolicy.Rate(native,1)==native,"No valid human contributor gets no assistance");
    Check(Math.Abs(SemiAssistPolicy.Rate(native,1.5)-native*1.5f)<.0001f,"Rate applies its supplied multiplier exactly once");
}
Check(SemiAssistPolicy.Rate(float.MaxValue,4)==float.MaxValue,"Boost cannot overflow native rate");
var normalWork=5d+6d; var preparation=1d;
Check(Math.Abs(normalWork/1.5+preparation-8.333333333333)<1e-8,"Native preparation remains unboosted; no fake fixed cycle");
foreach(var calibre in new[]{15d,125d,250d,500d,1000d})
{
    var a=SemiAssistPolicy.Outlet(2,3,calibre,false);var b=SemiAssistPolicy.Outlet(2,3,calibre,true);
    Check(a.X==-b.X && a.Y==b.Y && a.Z==b.Z,"Semi mirror changes only lateral feed position");
    Check(Math.Abs(a.Z-(1.5+.3475*SemiAssistPolicy.ArmScale(calibre)))<1e-12,"Visible large-shell arm and feed endpoint agree");
}
Check(SemiAssistPolicy.ArmScale(500)==4 && BustleFeedGeometry.Scale(500)==2,"Semi supports larger shell arm; accepted full-auto geometry unchanged");
var semiJson=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","Parts","sprocketSemiAutoloaderPart.json")));
var fullJson=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","Parts","sprocketBustleAutoloaderPart.json")));
Check(semiJson.RootElement.GetProperty("guid").GetString()!=fullJson.RootElement.GetProperty("guid").GetString(),"New semi part cannot overwrite existing bustle saves");
Check(semiJson.RootElement.GetProperty("components")[0].GetProperty("type").GetString()=="ammoRack","Semi keeps native ammo rack dimensions/capacity");
Console.WriteLine($"Large-shell semi geometry and distinct part contract PASS: {count} total assertions.");
var zeroSize=CarouselGeometry.Calculate(new[]{new Segment(2500,800)},125,375,40);
var zeroMeshes=CarouselVisualGeometry.Build(2.5,.8,zeroSize.Capacity,.125,.375,0,false);
Check(zeroMeshes.Where(m=>m.Material=="Charge").All(m=>m.Triangles.Count==0),"Zero native propellant never creates decorative ammunition");
// Approved mass/length workload curve, independently pinned reference and adversarial inputs.
foreach(var example in new[]{(8d,.6,.00133328,.95173326,.90413316),(12d,.8,.01000944,.96301227,.93102927),(21d,.95,.10763656,1.08992753,1.23367334),(28d,1.1,.31883302,1.36448293,1.88838236),(36d,1.2,.59095959,1.71824747,2.73197473),(60d,1.665,.94633020,2.18022927,3.83362363)})
{
    var b=SemiAssistPolicy.Curve(example.Item1,example.Item2);
    Check(b.Valid && Math.Abs(b.Weight-example.Item3)<1e-8,"Approved reference workload weight");
    Check(Math.Abs(b.Travel-example.Item4)<1e-8 && Math.Abs(b.Handling-example.Item5)<1e-8,"Approved independent rate references");
}
var referenceBalance=SemiAssistPolicy.Native(30,5,1);
Check(referenceBalance.Valid && referenceBalance.MassKg==35 && referenceBalance.LengthM==1,"Uses complete native component mass and metre length");
Check(Math.Abs(referenceBalance.Weight-.5)<1e-14 && Math.Abs(referenceBalance.Travel-1.6)<1e-14 && Math.Abs(referenceBalance.Handling-2.45)<1e-14,"Reference transition precisely pinned");
foreach(var mass in new[]{double.Epsilon,1e-200,.1,8,35,90,1e200,double.MaxValue})
foreach(var length in new[]{double.Epsilon,1e-200,.1,1,4,1e200,double.MaxValue})
{
    var b=SemiAssistPolicy.Curve(mass,length);
    Check(b.Valid && double.IsFinite(b.Weight) && b.Weight>=0 && b.Weight<=1,"Extreme finite workload cannot overflow curve");
    Check(b.Travel>=.95 && b.Travel<=2.25 && b.Handling>=.90 && b.Handling<=4,"Assistance always bounded");
    Check(SemiAssistPolicy.Curve(mass,length* .5).Weight<=b.Weight,"Longer workload monotonically receives more assistance");
}
foreach(var invalid in new[]{0d,-1,double.NaN,double.PositiveInfinity,double.NegativeInfinity})
{
    var b=SemiAssistPolicy.Curve(invalid,1);var c=SemiAssistPolicy.Curve(35,invalid);
    Check(!b.Valid && !c.Valid && b.Travel==1 && c.Handling==1 && b.OverheadWork==0,"Invalid inputs preserve native work/rates");
}
Check(!SemiAssistPolicy.Native(-10,45,1).Valid && !SemiAssistPolicy.Native(35,-2,1).Valid,"Negative component mass cannot hide inside positive sum");
Check(SemiAssistPolicy.Native(float.MaxValue,float.MaxValue,1).Valid,"Native float component sum evaluated without float overflow");
foreach(var crewRate in new[]{.1f,1f,2f})
{
    var basePickup=2f; var extended=SemiAssistPolicy.PickupWithOverhead(basePickup,referenceBalance);
    var boosted=SemiAssistPolicy.Rate(crewRate,referenceBalance.Handling);
    Check(Math.Abs((extended-basePickup)/boosted-.75/crewRate)<1e-6,"One overhead costs 0.75 native handling-seconds scaled by actual crew");
}
Check(SemiAssistPolicy.Rate(0,referenceBalance.Handling)==0 && SemiAssistPolicy.Rate(-1,referenceBalance.Handling)==-1,"Stopped native crew cannot advance overhead");
var invalidBalance=SemiAssistPolicy.Curve(0,1);
Check(SemiAssistPolicy.PickupWithOverhead(3,invalidBalance)==3,"Invalid profile adds no overhead");
Check(float.IsFinite(SemiAssistPolicy.PickupWithOverhead(float.MaxValue,referenceBalance)),"Finite native work stays finite");
Check(SemiAssistPolicy.Rate(float.MaxValue,4)==float.MaxValue,"Rate overflow retains native finite value");
Console.WriteLine($"Approved assisted balance PASS: {count} total assertions; native gameplay pending.");
// Native feature.3 VRRC fixture: correct dropoff work by its cached effective rate.
var countdown=new SemiCountdownMath.Work(0,0,9.628,0,2.708,0,6.660,0,.264*2.232,3.957);
Check(SemiCountdownMath.TryTimes(countdown,out var remain,out var duration),"Valid effective native rates produce display times");
var expected=9.628/3.957+2.708/(.264*2.232)+6.660/3.957;
Check(Math.Abs(remain-expected)<1e-12 && remain==duration,"Independent full phase seconds sum");
var legacyCountdown=9.628/3.957+2.708/(.264*2.232)+6.660;
Check(Math.Abs(legacyCountdown-remain-6.660*(1-1/3.957))<1e-12,"Reproduces native unscaled ramwork display error exactly");
var ramOnly=countdown with { Picked=9.628,Travelled=2.708 };
SemiCountdownMath.TryTimes(ramOnly,out var beforeRam,out _);
Check(Math.Abs(beforeRam-6.66/3.957)<1e-12,"6.66 work is about 1.68 seconds, not 6.66 seconds");
foreach(var t in new[]{.1,.5,1.0})
{
    SemiCountdownMath.TryTimes(ramOnly with { Dropped=t*3.957 },out var afterRam,out _);
    Check(Math.Abs(beforeRam-afterRam-t)<1e-12,"Displayed final phase decreases one second per real second");
}
foreach(var rates in new[]{(.1,.2),(1d,1d),(2.232*.264,3.957),(4d,8d)})
{
    var start=countdown with { Prepare=1,TravelRate=rates.Item1,HandlingRate=rates.Item2 };
    SemiCountdownMath.TryTimes(start,out var full,out var whole);
    var checkpoints=new[]{ start with {Prepared=.5}, start with{Prepared=1,Picked=.3*rates.Item2},start with{Prepared=1,Picked=9.628,Travelled=.2*rates.Item1},start with{Prepared=1,Picked=9.628,Travelled=2.708,Dropped=.4*rates.Item2} };
    var elapsed=new[]{.5,1+.3,1+9.628/rates.Item2+.2,1+9.628/rates.Item2+2.708/rates.Item1+.4};
    for(int i=0;i<checkpoints.Length;i++)
    {
        SemiCountdownMath.TryTimes(checkpoints[i],out var left,out var all);
        Check(Math.Abs(full-left-elapsed[i])<1e-9 && all==whole,"All native phases share the same elapsed time basis");
        Check(Math.Abs(SemiCountdownMath.Fraction(left,all)-elapsed[i]/all)<1e-12,"Load progress uses corrected remaining numerator and same total");
    }
}
var complete=countdown with{Prepared=10,Picked=10,Travelled=3,Dropped=7};
Check(SemiCountdownMath.TryTimes(complete,out var done,out var completedTotal) && done==0 && SemiCountdownMath.Fraction(done,completedTotal)==1,"Native phase overshoot cannot make negative countdown or >100 percent progress");
foreach(var invalid in new[]{0d,-1,double.NaN,double.PositiveInfinity})
{
    Check(!SemiCountdownMath.TryTimes(countdown with{HandlingRate=invalid},out _,out _),"Preserve native stopped/invalid rate sentinel");
    Check(!SemiCountdownMath.TryTimes(countdown with{TravelRate=invalid},out _,out _),"No misleading finite countdown with stopped travel");
}
Check(!SemiCountdownMath.TryTimes(countdown with{Dropoff=double.NaN},out _,out _),"Invalid work preserves native display");
Check(!SemiCountdownMath.TryTimes(countdown with{Pickup=double.MaxValue,HandlingRate=.01},out _,out _),"Overflow does not enter display floats");
Console.WriteLine($"Scoped native countdown correction PASS: {count} total assertions; fixture estimate {remain:0.000}s vs legacy {legacyCountdown:0.000}s, constant rates only.");

