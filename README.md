# Sprocket Carousel Autoloader

A vibe-coded BepInEx IL2CPP plugin that adds **carousel and bustle autoloaders** to Sprocket. Turn a turret basket into a T-72/90-style or T-64/80-style carousel, or place a compact bustle magazine behind a cannon.

**Built with AI assistance.** Version **0.2.9** adds the bustle part, crew magazine replenishment, nonlinear reload timing and custom mechanical sounds. The creator has tested automatic firing, replenishment, sounds and vehicle saving/loading in-game.

## Requirements

- Sprocket **0.2.55.5** (Unity **6000.3.21f1**).
- A working **Sprocket Mod Loader / BepInEx 6 IL2CPP** setup with its .NET 6 runtime—the same environment used by Hans21223's *Sprocket Quality of Life*.
- Quality of Life, [Shell Selector](https://github.com/RoanWassink/SprocketShellSelector) and [Material Selector](https://github.com/RoanWassink/SprocketMaterialSelector) are optional.

## Installation

1. Run the game once with the mod loader installed, then close it.
2. Download **SprocketAutoloaders-v0.2.9.zip** from [Releases](https://github.com/RoanWassink/SprocketCarouselAutoloader/releases/latest).
3. Extract its `BepInEx` and `Sprocket_Data` folders into your Sprocket installation. The resulting paths must be:

   ```text
   Sprocket\BepInEx\plugins\SprocketCarouselAutoloader.dll
   Sprocket\Sprocket_Data\StreamingAssets\Parts\roanBustleAutoloaderPart.json
   ```

4. Launch the game. The carousel controls appear in the turret basket editor; **Bustle autoloader** appears in the ammunition/parts palette.

Both files are required for the full release. The sounds are embedded in the DLL; no separate audio installation is needed. Replace the old DLL and install only one copy. Your original `ammoRackPart.json` stays as it is. No compiling needed. Back up your vehicle saves before experimenting and close the game before updates.

## Carousel setup

1. Select a turret basket and enable **Carousel autoloader**.
2. Choose **T-72 style: both horizontal** or **T-64/80 style: upright charge**.
3. Select the **Assigned cannon**. A single eligible cannon is selected automatically.
4. Adjust the turret ring and basket segments until the ammunition fits. The panel shows capacity, maximum propellant length, required diameter/depth and reload time.
5. Save the tank and press Play.

The carousel follows the cannon's ammunition selection. With Shell Selector, choose the shell profile on the cannon. It supplies its own finite ammunition rack, so ordinary racks and a crew loader are not needed to fire its initial stock. Once it empties, manual loading from ordinary racks needs a crew loader.

Use **Refresh ammunition information** after cannon changes if the panel still shows old dimensions. Supply readings in the inspector are snapshots, not a continuously updated battle counter.

## Bustle setup

1. Place **Bustle autoloader** under the same turret hierarchy as its cannon.
2. Open **Bustle autoloader (experimental)**, enable **Automatic loader** and select **Assigned cannon**.
3. Set the rack dimensions with the vanilla controls. These determine ready-magazine capacity. Ammunition follows the assigned cannon; do not select a different shell in the rack's vanilla ammunition control.
4. Point the visible feed arm toward the breech and check **Feed arm to breech**. It must be **less than 1.00 m** in the supported game version.
5. Add ordinary ammo racks with matching ammunition. To enable replenishment, create/place a crew member as usual and assign that crew member the loader role for the same cannon in the normal Sprocket crew controls. The initial magazine can fire without one.
6. Save, load and test Play.

There is no separate autoloader-refill role or button: the normal cannon loader automatically replenishes the bustle after its ready magazine empties. The crew transfers reserve ammunition **one complete shot at a time**. Each completed transfer can be used immediately; the gun does not have to wait for a full magazine. Crew work and reserve-to-magazine travel affect replenishment speed, while the mechanical magazine-to-gun cycle follows its own reload time. A commander can also carry the loader role, subject to the native crew allocation and reach checks.

### How bustle distance works

The distance starts at the **front tip of the visible loading fork**, rather than at the centre of the rack, and ends at the cannon's native loading point near the breech. Rack size, rotation, scale and current component transforms are included. The arm projects along the part's local **+Z** direction: rotate the part so the arm faces the cannon.

The limit uses Sprocket's native crew-hand loading reach, currently **1.00 m**. This is a hard eligibility check, not a reload-time penalty. A reading of `0.75 / 1.00 m` is in range; `1.26 / 1.00 m` is out of range. The comparison is strict: aim below the limit, since a rounded `1.00` can already be outside it.

The rack body retains its physical collision. The decorative arm has no added collision, so it can bridge the reserved loading space without pushing the rack body into the gun. Only the enabled rack's assigned-cannon breech/load-area *RequiredSpace* checks receive an exemption. Other colliders still apply. There is no simulated arm alignment, obstacle routing or recoil clearance animation.

See [BUSTLE.md](BUSTLE.md) for the complete setup, replenishment and placement explanation.

## Storage and reload balance

| Layout | Arrangement | Main constraint |
|---|---|---|
| T-72/90 carousel | Projectile and charge in two horizontal layers | The longer component determines radial space |
| T-64/80 carousel | Horizontal projectile body and upright charge | Larger charges need more depth, rather than more radial space |
| Bustle | Native ammunition rack with a mechanical feed | Native dimensions/capacity, same turret and feed-arm reach |

Carousel diameter uses the **smallest turret-ring or segment diameter**; depth uses the **sum of segment depths**, with component scaling. Reference dimensions give about **22 horizontal-cassette shots or 28 upright-charge shots**. Larger carousels can hold more, up to 64 complete shots.

Both systems use the same nonlinear calibre, length and mass curve. A **120 mm cannon with 1,200 mm propellant**, 1,560 mm native combined round length and 44.1 kg per shot has a **6.002-second** mechanical cycle. Compact 25 mm and 30 mm examples give about 0.140 and 0.190 seconds. These are balance examples, not historical specifications; native firing/recoil and frame timing can reduce the actual rate.

See [BALANCE.md](BALANCE.md) for formulas and reference dimensions.

## Sounds

Each mechanical reload of at least one second uses the corresponding custom sound:

- T-72/90 horizontal cassette: `t90.wav`.
- T-64/80 upright charge: `t64.wav`.
- Bustle: its own mechanical sound.

The two Russian clips have identical left/right channels and levels matched to the bustle sound. Longer sounds speed up to fit the cycle, up to 2x, and stop when loading completes. Rapid autocannons and waiting for crew replenishment receive no added mechanical cue.

## Saves and compatibility

Enabled state, assigned cannon and carousel layout are stored in the vehicle blueprint. A new Play session starts with normal filled design stock; battle ammunition remaining is not saved. Check cannon assignments after copying or mirroring parts. One cannon uses one selected automatic feed; selecting a bustle replaces its carousel assignment, and vice versa.

Old carousel saves from v0.1.8 remain supported. Earlier development saves may need their autoloader settings enabled and saved again. Keep the plugin and custom part installed to load vehicles using the bustle part.

## Current limitations

- Carousel mechanics are virtual; bustle has a static frame and arm. No carousel rotation, lift, belt or rammer animation.
- Native bustle rack damage is retained; no dedicated carousel collider, blast doors or blow-out panels are implemented.
- No added carousel mechanism cost or internal fit/collision simulation.
- Design mass includes the mechanism and full ammunition load; it stays constant during firing.
- Timing and storage dimensions are gameplay approximations.

The controls retain their **experimental** label. Compatibility with other game versions is untested.

## Troubleshooting

**Bustle part missing:** install `roanBustleAutoloaderPart.json` in the path above and restart the game.

**Outside native crew-loader reach:** move or rotate the bustle so its fork tip is closer to the breech, keep the rack under the same turret and target a reading below 1.00 m. Increasing magazine capacity does not remove the reach limit.

**Ready magazine empty and no refill:** check matching reserve ammunition, an assigned healthy crew loader and native hand reach to the magazine. Logs report missing crew, invalid reach, an idle loader task or missing matching reserve stock. No crew means no replenishment.

**Carousel ammunition does not fit:** check required diameter/depth. A narrower segment still limits a larger ring; upright-charge overflow needs deeper segments. A non-fitting carousel falls back to ordinary loading.

**No mechanical sound:** cycles shorter than one second and crew-refill waiting are intentionally silent. Check the plugin version and audio warnings in the log.

Check `Sprocket\BepInEx\LogOutput.log` for `Sprocket Carousel Autoloader`, `[Carousel]`, `[Bustle]` or `[Autoloaders]`. [Report issues](https://github.com/RoanWassink/SprocketCarouselAutoloader/issues) with game/plugin versions, cannon settings, selected feed/layout, dimensions, distance reading and relevant log lines.

## Building from source

Install the .NET 8 SDK and generate local mod-loader interop assemblies by starting the game once.

```powershell
dotnet build CarouselAutoloader.csproj -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Sprocket"
dotnet run --project tests/Carousel.Tests.csproj -c Release
```

The DLL targets net6.0 x64. Game/loader assemblies are referenced locally and are not redistributed. The tests cover 53,664 geometry, timing and WAV assertions; startup checks native finite storage/transfer, loader integration, audio upload and blueprint serialization. These complement gameplay testing.

`Install.ps1` installs the built DLL and bustle part with the game closed and backs up existing copies. It assumes the source checkout is `Sprocket\Mods\CarouselAutoloader`; otherwise install the two files manually.

## Credits

Created by RoanWassink with AI assistance, including the custom reload sounds. Uses Sprocket's native ammunition storage/loading through BepInEx 6 IL2CPP and Harmony. The mod-loader environment is also used by Hans21223's *Sprocket Quality of Life*.

Released under the [MIT License](LICENSE).

## Donations

For ChatGPT budget. Helps me reverse engineer Sprocket to add cool mods.

[Donate via PayPal](https://www.paypal.com/donate/?hosted_button_id=7PE3SDBETXFQ6)
