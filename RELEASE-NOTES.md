# Sprocket Carousel Autoloader v0.2.12 — beta

- **Mirror feed arm:** move the bustle rammer and fork to the opposite side. Feed distance follows the selected fork, and each part remembers its setting when saving and loading.
- **Compatible settings migration:** existing vehicle saves remain readable. New saves use neutral settings identifiers; the part GUID remains unchanged. An old configuration is copied only when the new configuration is absent.

Automatic loading, finite ammunition, crew replenishment, reload balance and the Howden/Russian recordings are unchanged.

**Update:** replace the existing DLL. Copy `sprocketBustleAutoloaderPart.json`, then remove the older `roanBustleAutoloaderPart.json` from the same Parts folder. Keep backups of your vehicles and settings, and keep only one DLL version. Older plugin versions cannot read the newly named save settings; use pre-update vehicle backups when rolling back.

Requires Sprocket 0.2.55.5 and a working Sprocket Mod Loader / BepInEx 6 IL2CPP setup. The loader is separate; Quality of Life is optional. See README for installation and usage.

[Support my ChatGPT budget and help me reverse engineer Sprocket to make more mods](https://www.paypal.com/donate/?hosted_button_id=7PE3SDBETXFQ6).

<!-- sp-compat {"hamish.sprocket": "0.2.55.5", "bepinex.bepinex": "6.0.0-be.788"} -->
