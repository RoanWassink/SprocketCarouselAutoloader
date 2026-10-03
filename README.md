# Sprocket Carousel Autoloader

A vibe-coded BepInEx IL2CPP plugin that turns Sprocket's **turret basket into a carousel autoloader**. Choose a T-72-style horizontal cassette or a T-64/80-style upright charge layout, assign a cannon and let basket dimensions determine ammunition capacity.

**Built with AI assistance.** Automatic loading without a crew loader, firing and vehicle saving/loading have been tested in-game. Version **0.1.8** is the first public release.

## Requirements

- Sprocket **0.2.55.5** (Unity **6000.3.21f1**).
- A working **Sprocket Mod Loader / BepInEx 6 IL2CPP** setup with its .NET 6 runtime—the same environment used by Hans21223's *Sprocket Quality of Life*.
- Quality of Life, [Shell Selector](https://github.com/RoanWassink/SprocketShellSelector) and [Material Selector](https://github.com/RoanWassink/SprocketMaterialSelector) are optional.

## Installation

1. Run the game once with the mod loader installed, then close it.
2. Download **SprocketCarouselAutoloader.dll** from this repository's [Releases](https://github.com/RoanWassink/SprocketCarouselAutoloader/releases) section. The ZIP includes the same DLL and documentation.
3. Drop the DLL into:

   ```text
   Sprocket\BepInEx\plugins\
   ```

4. Launch the game, select a turret basket and open **Carousel autoloader (experimental)**.

No compiling needed. Back up your vehicle saves before experimenting. Install only one copy of this plugin. Close the game before replacing the DLL.

## Setting up an autoloader

1. Enable **Carousel autoloader** in the turret basket inspector.
2. Choose the **Storage layout**.
3. Select the **Assigned cannon**. A single eligible cannon is selected automatically; tanks with multiple cannons can choose which one this carousel feeds.
4. Adjust the turret ring and basket segments until the ammunition fits. The inspector shows capacity, storage dimensions, maximum propellant length, required basket dimensions and mechanical reload time.
5. Save the tank and press Play.

The carousel follows its assigned cannon's ammunition. If you use Shell Selector, choose the shell profile on the cannon. There is no separate carousel ammunition selector.

A fitting carousel supplies its own finite ammunition rack in Play, including on tanks without ordinary ammo racks. You do **not** need to assign a crew member as its loader. Other crew roles still work normally. After the carousel is empty, manual loading from ordinary racks requires a crew loader.

Use **Refresh ammunition information** after changing cannon settings if the basket inspector still shows old dimensions. **Last test supply** is the stock read during an inspector redraw, rather than a continuously updated ammunition counter.

## Storage layouts and balance

| Layout | Storage arrangement | Main constraint |
|---|---|---|
| T-72 style: both horizontal | Projectile and charge lie in two horizontal layers | The longer component determines radial space |
| T-64/80 style: upright charge | Projectile lies horizontally; charge stands upright | Projectile body determines radial space; larger charges need more depth |

Available diameter is the **smallest diameter of the turret ring and all basket segments**. Available depth is the **sum of all segment depths**. Component scaling is included.

The model is scalable: reference dimensions give approximately **22 T-72-style shots or 28 T-64/80-style shots**. Larger designs can store more, up to 64 complete shots. Extra depth does not create extra carousel rings.

Sprocket's native round length does not directly describe real Soviet separate-loading ammunition. This plugin uses an explicit gameplay storage model while retaining the cannon's native ammunition, mass and ballistics. Layout names describe the arrangement, rather than an exact reproduction of a particular tank.

See [Storage and balance details](BALANCE.md) for the formulas, reference dimensions and examples.

## Vehicle saves

The enabled state, storage layout and assigned cannon are saved inside the vehicle blueprint. There is no separate per-tank configuration file. A new Play session starts with a filled carousel; this does not save ammunition remaining in an ongoing battle.

Blueprints saved with development versions before 0.1.8 may lack these settings. Set up the carousel once more and save the tank again. Check the assigned cannon after copying or mirroring components.

## Current limitations

- No visible carousel model, rotation, lift or ramming animation.
- No dedicated carousel damage collider or ammunition cook-off simulation.
- No mechanism cost or collision check against other parts inside the basket.
- No dedicated in-battle carousel ammunition display.
- Design mass includes the mechanism and a full ammunition load; it does not decrease as rounds are fired.
- Reload timing and storage dimensions are gameplay approximations.

The inspector retains its **experimental** label. Compatibility with other game versions has not been established.

## Troubleshooting

**Ammunition does not fit:** read the required diameter and depth in the inspector. Increasing the ring alone will not help if a basket segment remains narrower. For the T-64/80 layout, oversized propellant settings increase the required depth.

**The carousel is empty:** a new Play session should start with a filled, fitting carousel. Once its finite stock is used, add ordinary ammo racks and assign a loader for manual loading. A non-fitting carousel falls back to ordinary loading.

**Settings disappear after loading:** check that v0.1.8 or newer is installed, enable the carousel and save again. Older development saves cannot recover settings that were never written.

Check `Sprocket\BepInEx\LogOutput.log` for messages containing `Sprocket Carousel Autoloader` or `[Carousel]`. Successful saves report **Saved basket**; enabled carousels read from a blueprint report **Restored basket**.

When [reporting an issue](https://github.com/RoanWassink/SprocketCarouselAutoloader/issues), include your game/plugin versions, selected layout, cannon settings, ring/segment dimensions, what you did and the relevant log lines.

## Building from source

For contributors: install the .NET 8 SDK and start the game once with the working mod loader so `BepInEx\interop` exists.

```powershell
dotnet build CarouselAutoloader.csproj -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Sprocket"
```

The output is `bin\Release\net6.0\SprocketCarouselAutoloader.dll`. The included `Install.ps1` installs the built DLL with the game closed and backs up the existing plugin. It assumes this source directory is located at `Sprocket\Mods\CarouselAutoloader`; for other checkouts, copy the DLL manually.

Game and loader assemblies are referenced from your local installation and are not included here.

Run the independent geometry regression checks with:

```powershell
dotnet run --project tests/Carousel.Tests.csproj -c Release
```

There are **52,963 geometry assertions**. Startup also checks native finite storage, the loader contributor interface and 12 settings round-trips through Sprocket's JSON serializer. These checks complement the in-game tests; they do not replace gameplay validation.

## Credits

Created by RoanWassink with AI assistance. Uses Sprocket's native ammunition storage and loading system through BepInEx 6 IL2CPP and Harmony. The mod-loader environment is also used by Hans21223's *Sprocket Quality of Life*.

Released under the [MIT License](LICENSE).
