# Bustle autoloader

The bustle is a separate placeable part with its own icon, a black frame and a static loading arm around Sprocket's native ammo-rack model. It holds a finite ready magazine and follows its assigned cannon's ammunition.

Install both the DLL and `Parts/roanBustleAutoloaderPart.json` from the release ZIP as described in [README.md](README.md). The sounds are embedded in the DLL.

## Placement and assignment

Place **Bustle autoloader** under the same turret hierarchy as the cannon. Enable **Automatic loader** in the part editor, select **Assigned cannon**, then use the vanilla rack dimensions to choose magazine size/capacity. Hull racks cannot automatically feed a turret cannon. Choose the shell profile on the cannon; the bustle synchronizes its ammunition to that selection.

Rotate the arm toward the breech. It points along the part's local +Z direction. The decorative arm/fork adds no colliders, but the actual rack body retains native physical and damage collision. Its frame and fork follow the rack bounds and calibre.

## Feed arm to breech: distance explained

**Feed arm to breech** is the native component-path distance between the front of the visible loading fork and the cannon's native loading position. It starts at the fork tip, not at the rack centre or crew seat. The endpoints include rack dimensions, rotation, scale and current component transforms.

The maximum comes from `CannonBreech.MinimumOperateHandDistance`, Sprocket's native hand-reach limit for a crew loader. In the supported game version it is **1.00 m**. Automatic feed requires a distance **strictly below** that limit.

| Panel reading | Result |
|---|---|
| `0.75 / 1.00 m` | Within reach, provided the other eligibility checks pass |
| `1.26 / 1.00 m` | Automatic feed unavailable; move or rotate the arm closer |
| Rounded `1.00 / 1.00 m` | Could be outside; leave a small margin below the limit |

Distance does **not** add time to the mechanical reload. Both a short and a long in-range feed use the same ammunition-based cycle. Outside reach, ordinary manual-loading rules apply if a suitable native crew loader and supply exist.

An enabled bustle has a scoped exemption from its assigned cannon's breech/load-area *RequiredSpace* obstruction checks. It is not exempt from every physical collider. Place the rack body clear of solid collision and let the projecting fork bridge the reserved space. The game still requires the same turret hierarchy, valid ammunition, a healthy installed rack and positive design capacity. The plugin does not test arm angle, route around obstacles or simulate retraction during recoil.

## Ready magazine and crew replenishment

The magazine starts with its native finite stock in a new Play session. Mechanical loading from that stock needs no human loader.

To replenish it, add ordinary racks with the same ammunition. Create/place a crew member exactly as for a normal manually loaded tank, then assign that crew member the **loader role for the assigned cannon** using Sprocket's normal crew controls. No separate refill role, magazine assignment or refill button is needed: that normal loader automatically starts replenishing the bustle when its ready magazine empties. The role's operating point is moved to the bustle magazine while the automatic feed is enabled; native crew hand reach, health, task allocation and efficiency still apply. A commander may also load, subject to those checks.

Once the magazine empties, the crew begins replenishing it **one shot at a time**. Each completed transfer removes one shot from an ordinary rack and adds one to the magazine. The cannon can load/fire after that transfer without waiting for the magazine to fill. Replenishment continues while crew task allocation permits, until the magazine is full or reserves run out. Stock is conserved; ammunition is not generated.

Reserve-to-magazine travel, ammunition handling and crew efficiency affect refill time. This crew journey is separate from the mechanical fork-to-breech distance and its hard reach limit. An empty magazine waits for replenishment instead of silently bypassing it to load from a reserve rack.

## Reload timing and mass

The same curve is used for carousel and bustle mechanics:

```text
blend          = clamp((calibre_mm - 40) / 80, 0, 1)
smooth_blend   = blend² × (3 - 2 × blend)
coefficient    = 8.71 + (6.18 - 8.71) × smooth_blend
calibre_scale  = (calibre_mm / 125)^2.4
handling       = coefficient × calibre_scale × (native_length_mm / 1341)^0.4
                 × (0.9 + 0.1 × sqrt(native_mass_kg / 45.8))
reload_seconds = 0.06 + handling
mechanism_kg   = 120 + 5 × native rack capacity
```

The mechanism mass is added to the native rack baseline. Examples:

| Calibre | Combined length | Complete-shot mass | Mechanical cycle | Theoretical feed rate |
|---|---|---|---|---|
| 25 mm | 212 mm | 0.5 kg | 0.140 s | 430/min |
| 30 mm | 240 mm | 0.8 kg | 0.190 s | 316/min |
| 120 mm | 1,560 mm (1,200 mm propellant) | 44.1 kg | 6.002 s | 10/min |
| 125 mm | 1,341 mm | 45.8 kg | 6.240 s | 10/min |

Native gun firing/recoil and frame timing can lower the actual firing rate. These are gameplay reference points, not historical weapon specifications.

## Sounds and saves

The bustle uses its own mechanical WAV; carousel sounds depend on layout. Sounds play once per actual mechanical cycle of at least one second, with no added cue for rapid autocannons or crew-refill waiting. Longer sounds speed up to fit the reload, up to 2x, and stop when loading completes.

Enabled state and cannon assignment are saved in the vehicle blueprint. A new Play session fills normal design stock; it does not restore battle ammunition remaining. One selected automatic feed serves each cannon.

## Troubleshooting and limitations

A missing part usually means the custom part JSON was not installed. No automatic feed can mean wrong turret parenting, distance outside reach, damaged/disabled rack or mismatched ammunition. No replenishment can mean missing crew, invalid native reach, an inactive loader task or no matching reserve ammunition. Read the `[Bustle]` messages in `BepInEx/LogOutput.log`, including wait, transfer and resume messages.

The arm is static. There are no conveyors, rammer/recoil animations, blast doors or blow-out panels. Native rack damage is retained. No physical route/alignment simulation is implemented. This crew-replenishment feature applies to the bustle ready magazine; an empty carousel currently falls back to ordinary manual loading.

The creator confirmed firing, crew replenishment and sounds in-game through v0.2.9. Native startup and managed regression checks supplement those tests. The inspector remains marked experimental and other game versions are untested.
