# Changelog

## v0.2.10 — Performance update

- Reduce repeated vehicle/component scans during autoloader updates, including checks on ordinary cannons.
- Refresh automatic loader contributions once per frame instead of repeating global work for each vehicle controller.
- Reuse bustle distance checks and reserve-ammunition paths; rebuild cached connections when parts rebuild or enable/disable.
- Skip rapid autocannon substeps and mechanical audio lookups while idle or waiting for crew replenishment.
- Keep reload timing, magazine capacity, crew requirements, sounds and vehicle save keys unchanged.

Release build, 53,674 managed assertions and native startup checks pass. The creator tested this update in-game and confirmed the previously reported FPS drops no longer occur. No fixed numerical FPS gain is claimed.

## v0.2.9 — Carousel and bustle autoloaders

- Add a separate placeable bustle ready magazine, custom icon, black frame and static feed arm.
- Use native rack dimensions/capacity and follow the assigned cannon's ammunition.
- Measure bustle reach from the fork tip to the cannon loading point, with the native crew-loader reach as a hard limit (currently less than 1.00 m). Distance does not slow mechanical reloads.
- Let an assigned crew loader replenish an empty bustle from matching ordinary racks, one conserved shot at a time. Each shot is usable immediately.
- Use nonlinear calibre/length/mass timing for both feeds: about six seconds for the 120 mm / 1,200 mm propellant reference, with faster compact autocannon cycles.
- Add layout-specific mechanical reload sounds, with Russian clip levels matched to bustle and right-channel audio copied to the left. Rapid autocannons remain silent.
- Preserve carousel/bustle settings in vehicle saves and automatic firing without a crew loader while ready stock remains.
- Guard missing native turret-audio behaviours and keep optional audio/visual hooks separate from gameplay.
- Document installation, fork-to-breech placement, crew reach/refill, balance and troubleshooting. Include geometry, timing and WAV checks.

The creator confirmed the final functionality and sounds in-game. Intermediate v0.2.x builds were local tests.
## v0.1.8 — First public release

- Turn a turret basket into a carousel autoloader with T-72 or T-64/80 storage layouts.
- Assign a cannon and follow its ammunition selection.
- Calculate finite capacity, fit limits, mass and reload time from cannon and basket geometry.
- Supply a native ammunition rack without requiring ordinary racks or a crew loader.
- Save and restore enabled state, layout and assigned cannon in vehicle blueprints.
- Show maximum propellant length and required basket diameter/depth in the inspector.
- Include managed geometry checks and native startup checks.

Earlier versions were local development builds. In-game testing confirmed automatic loading, firing and vehicle saving/loading before this public release.
