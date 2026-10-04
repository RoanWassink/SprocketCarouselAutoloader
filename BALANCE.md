# Storage and balance

These formulas describe the v0.2.9 gameplay model. They do not predict real projectile or propellant dimensions. They affect storage and loading, without replacing the cannon's native ammunition or ballistics.

All lengths below are in millimetres unless otherwise stated. `c` is the cannon's full calibre, `p` its native propellant-length setting, and `L = 3c + p` the native combined round length.

## Available space

- Diameter `D`: the smallest of the turret ring and every basket segment diameter. The implicit diameter of the first segment follows the ring.
- Depth: the sum of the basket segment depths.
- Component scale is applied before the fit and capacity calculations.
- Outer wall clearance: 40 mm.
- Central drive radius: `max(180, 0.12D)`.

The available radial length runs from the central drive to the outer clearance. Minimum diameter and maximum native propellant values in the inspector are derived from the same fit model; a depth restriction is shown separately.

## T-72-style horizontal cassette

```text
Stored charge length     = min(p, 408c / 125)
Stored projectile length = L - stored charge length
Radial footprint         = max(stored projectile length, stored charge length)
Required depth           = 150 + 18 + c + 1.28c
```

Both components lie horizontally, one above the other. Charges longer than the reference allocation make the projectile allocation longer in this abstract model. Native propellant is not capped or removed from the ammunition.

At 125 mm calibre, required depth is 453 mm. A native 1,341 mm round becomes a 933 mm projectile allocation and a 408 mm charge allocation.

## T-64/80-style upright charge

```text
Stored projectile body = 680c / 125
Reference charge       = 408c / 125
Stored charge length   = min(p, reference charge)
                         + max(0, L - stored projectile body - reference charge)
Radial footprint       = stored projectile body
Required depth         = 150 + 18 + c + max(stored charge length, 1.28c)
```

At a fixed calibre, oversized propellant settings increase height without expanding the projectile body's radial footprint. Very short native rounds still reserve the fixed body and minimum charge thickness; storage dimensions need not sum to the native length in this layout.

At 125 mm calibre and native length 1,341 mm, the storage model uses a 680 mm body and 661 mm upright charge. Required depth is 954 mm.

## Capacity

Cassette width is `1.28c + 12`. Capacity uses circumference at the average storage radius, with an effective mechanical pitch of **1.40 × cassette width** for T-72 or **1.12 × cassette width** for T-64/80. The radius depends on the radial footprint and available outer radius; see [CarouselGeometry.cs](CarouselGeometry.cs) for the complete calculation.

The pitch factors calibrate space for cassettes and mechanisms. They are not a proof that rectangular rounds fit without collisions. Using the outside circumference divided by calibre alone would overestimate capacity.

| Usable diameter | Calibre | Native propellant | T-72 | T-64/80 |
|---|---|---|---|---|
| 2,500 mm | 125 mm | 713 mm | 22 shots; 453 mm required depth | 28 shots; 701 mm required depth |
| 2,716 mm | 125 mm | 966 mm | 22 shots; 453 mm required depth | 31 shots; 954 mm required depth |

These examples assume sufficient basket depth. Larger designs may carry more than historical reference capacities, up to 64 complete shots. Increasing depth alone does not add another storage ring.

## Mass and reload time

```text
Mechanism mass (kg) = 120 + 40 × diameter in metres + 5 × capacity
Reload time uses the nonlinear calibre/length curve documented in BUSTLE.md.
```

The design includes the mechanism and full native ammunition mass. Design mass remains constant during firing. Reload time represents an abstract mechanical cycle, without separate rotation/lift/ram animations.
