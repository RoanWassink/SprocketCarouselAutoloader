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
