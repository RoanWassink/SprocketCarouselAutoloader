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

foreach (var audio in new[] { ("t90", 44100, 283768), ("t64", 44100, 356970), ("bustle", 48000, 240000) })
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
