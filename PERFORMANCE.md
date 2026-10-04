# v0.2.10 performance update

This local build replaces repeated per-update vehicle scans with a cannon connection index, including cached negative lookups for ordinary cannons. The global automatic-contributor refresh runs once per frame, rather than once per vehicle controller over every vehicle's bindings. Snapshots change only when bindings attach/detach. Bustle spatial/timing checks are shared within a frame; health, installed state, ammunition stock and crew efficiency remain live checks.

Ready/full magazines skip crew work. A transfer keeps its valid source and reuses its native path, choosing a new nearest reserve only at a shot boundary or when the source becomes unavailable. Rapid autocannon substeps run only while mechanically loading, never while idle or waiting for crew. Audio avoids mechanical lookup for idle tasks. Timing, reach limit, magazine capacities, sounds and save keys are unchanged.

The release build and 53,674 assertions pass. A policy regression with 100 controllers across three frames performs three global refreshes instead of 300. Native startup confirms all six cache invalidation hooks, finite storage/transfer, blueprint JSON and the same custom audio uploads. This is not an in-game FPS benchmark.

For comparison, use the same saved tank/scenario, number of AI vehicles and camera position with v0.2.9 and this build. Compare idle, sustained firing and empty-magazine replenishment. Include a compact autocannon if available. Check repeated Play/menu transitions, save/reload, last-round replenishment and audio. Swap DLLs only with the game closed; the installer retained the prior DLL in backups. Report FPS and vehicle count alongside any errors in BepInEx/LogOutput.log.

The creator subsequently tested this update in-game and confirmed the previously reported FPS drops no longer occur in their test. No fixed numerical FPS improvement is claimed, and other tanks and scenarios may behave differently.

The update requires no configuration changes. Install the public release package with the game closed. The automated tests above establish reduced scheduling work and complement the creator's gameplay test.
