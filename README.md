# Sprocket CarouselAutoloader

## New in 0.2.14

- **Static Russian-style carousel model:** the basket contains visible ammunition that follows the native magazine stock.
- **Crew-required assisted loader:** a placeable ready magazine and mechanical loading aid for large ammunition. Assistance scales smoothly with the full cartridge's native mass and length. Smaller, lighter rounds can be faster by hand.
- **Distinct assisted-loader icon:** an autoloader icon plus a crew silhouette identifies the crew-operated option.
- **Accurate assisted countdown:** displayed time and progress account for the real handling speed throughout the cycle.

### Set up an assisted loader

Place the assisted-loader part, assign its ammunition reference to the intended cannon, and give that cannon a healthy working crew loader. Keep its feed endpoint within the native loading reach, on the same turret. Set the native rack size and fill: stock remains finite. The mechanical handoff, distance, crew efficiency and preparation all contribute to loading time; it has no guaranteed reload time per calibre. Replenishment uses compatible ordinary reserve ammunition and real crew work.

The carousel appearance is static; rotating or moving-shell animations are not included. Assisted loading does not remove the need for crew or create ammunition.


Carousel and bustle autoloaders with finite ammunition, automatic loading and distinct mechanical sounds.

**v0.2.14 — beta.** Mirror the bustle feed arm to fit either side of your turret. Existing vehicle settings are read automatically; newly saved settings use neutral identifiers. Automatic loading, finite magazines, sounds and reload balance are retained.

## Requirements

- Sprocket **0.2.55.5**, Windows x64, Unity 6000.3.21f1.
- A working **Sprocket Mod Loader / BepInEx 6 IL2CPP (6.0.0-be.788)** setup with its runtime and generated interop. Loader installation is separate. Stock BepInEx alone is not claimed equivalent to the tested Sprocket-specific setup.
- Quality of Life is not required or included. Other game versions have not been verified.

## Install and update

1. Install a working Sprocket Mod Loader / BepInEx 6 IL2CPP setup, run Sprocket once, then close it. The loader is a separate prerequisite and is not included.
2. Download **SprocketCarouselAutoloader-v0.2.14.zip** from [this release](https://github.com/RoanWassink/SprocketCarouselAutoloader/releases/tag/v0.2.14).
3. In Steam, use Sprocket > Manage > Browse local files. Copy the ZIP's folders into the folder containing Sprocket.exe. Merge folders; keep the internal structure intact.
4. Keep one copy of each plugin. Back up matching mod files and vehicle saves before updating. Never replace the whole BepInEx folder.
5. Preserve existing BepInEx/config files, customized files. Install required dependencies separately. Restart the game.

## Updating from older versions

Replace the DLL rather than keeping both versions. The plugin identifier is now `sprocket.carousel`. If `BepInEx/config/sprocket.carousel.cfg` is absent, the old `nl.roan.sprocket.carousel.cfg` is copied without changing the original. An existing new configuration always wins.

The part file is now `Sprocket_Data/StreamingAssets/Parts/sprocketBustleAutoloaderPart.json`. After copying the new file, remove the old `roanBustleAutoloaderPart.json` from that same Parts folder to avoid duplicate part definitions. The part GUID stays unchanged, so existing vehicles retain the part.

Older vehicle saves remain readable. New saves use canonical settings; keep a pre-update save backup if you need to roll back to an older plugin.

## Usage, controls and settings

Select a turret basket and open Carousel autoloader; choose the horizontal or upright layout and assigned cannon. Capacity/fit depend on ammunition and basket dimensions. A separate bustle autoloader part is also included. Select it and toggle **Mirror feed arm** to move its fork to the opposite side. This setting is saved independently per part. The displayed breech distance is measured from that fork; keep it within the existing 1.00 m reach limit. Native paired mirror placement does not automatically toggle this setting. Each automatic magazine is finite; ordinary ammo racks and crew loading are needed for supported replenishment/manual loading. The bustle uses the updated Howden recording; carousel layouts retain their own sounds. Read [the complete fitting and refill guide](REFERENCE-GUIDE.md). Disable autoloaders/remove the bustle part and save before uninstalling.

## Troubleshooting, saves and rollback

If the mod is absent, check BepInEx/LogOutput.log for the mod name, missing dependencies, duplicate plugin versions or invalid configuration. Preserve a malformed file for inspection instead of overwriting all your settings. Restart after repairs.

Restore your backed-up mod files and settings together for rollback. Do not delete an entire shared folder. Custom parts/materials may be referenced by vehicle saves: return affected vehicles to stock parts/materials and save before uninstalling. Keep save backups; installed mods and release archives do not back up every vehicle automatically.

## Credits and support

Made with AI assistance. Mod code is MIT licensed; native Sprocket meshes/icons are resolved from your installed game and are not bundled. Donation: [Support my ChatGPT budget and help me reverse engineer Sprocket to make more mods](https://www.paypal.com/donate/?hosted_button_id=7PE3SDBETXFQ6).

## Where to get the separate loader

Use [Hans21223's Sprocket Mod Loader](https://github.com/Hans21223/Sprocket-Mod-Loader) and follow its [manual installation guide](https://github.com/Hans21223/Sprocket-Mod-Loader/blob/main/package/MANUAL-INSTALL.md) or its documented manager installation. That upstream project targets the tested Sprocket version and supplies the Sprocket-specific patch. These mod downloads do not install the loader. Follow one upstream loader method and its update/backup instructions; the creator's supplied ModManager archive is not redistributed here.
