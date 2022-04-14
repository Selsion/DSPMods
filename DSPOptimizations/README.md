# DSPOptimizations Mod
This mod adds optimizations to DSP. Few optimizations are available currently, but more will be added in the future.

## Important Notice
The low resolution shells optimization is now obsolete with DSP v0.9. If you had lower resolution shells in your save, then the vanilla game will update them incorrectly. As of v1.0.6, this mod should update such modded shells correctly.

## Features
- Dense spheres with shells now impact the game's framerate much less
	- e.g. the framerate with a single sphere layer with 5240 shells with the game paused went from 8 fps to 24 fps
- Multithreading is added for the logic controlling belts going in and out of stations
	- this should greatly reduce the CPU time under the "Storage" category in the performance window
- Dyson node logic has been optimized to take 20% as long
- Hidden sphere layers under construction cause less lag
- Shadows can be disabled in the config

## Commands
The following commands may be used with the [developer console](https://dsp-wiki.com/Developer_Console):
- `-resetLocalFactory`
	- completely resets the factory data for the nearby planet. This can eliminate lag that would have persisted even after deleting all buildings
	- requires a planet to be loaded
	- does not reset terrain, vegetation, or veins
- `-resetLocalSwarm`
	- completely resets all swarms for the nearby star, deleting all sails. This can eliminate permanent save bloat
	- requires the player to be near a star (within ~2 lyrs - the star name should be visible in the bottom left)
- `-resetLocalSphereLayer layerId`
	- instantly deletes a sphere layer without releasing sails
	- requires the player to be near a star (within ~2 lyrs - the star name should be visible in the bottom left)
	- `layerId` should be an integer between 1 and 10 corresponding to the ID of the sphere layer you wish to delete
- `-resetAllSpheresAndSwarms`
	- instantly deletes all dyson spheres and swarms for all stars. No sails will be released

## Installation
This mod has a [Thunderstore release](https://dsp.thunderstore.io/package/Selsion/DSPOptimizations/). It's recommended that you install it with [r2modman](https://dsp.thunderstore.io/package/ebkr/r2modman/) or another mod manager.

### Manual Installation
You will first need to download the mod package, which can be found at the [releases](https://github.com/Selsion/DSPMods/releases) page. This mod requires BepInEx to be installed in order to work, so install that if you haven't already. This mod depends on [BepInEx v5.4.17](https://dsp.thunderstore.io/package/xiaoye97/BepInEx/), but will likely work with earlier versions. If you installed BepInEx manually, then you should see the BepInEx folder in the game's steam directory. If you're using r2modman, then the path can be found by navigating to the settings page and clicking on "Browse profile folder". Alternatively, the path can be found at `%appdata%\r2modmanPlus-local\DysonSphereProgram\profiles\[PROFILE_NAME]\BepInEx`, where `[PROFILE_NAME]` is replaced with the name you set when you created the profile.

Inside the mod package, you should see two folders named `plugins` and `patchers`. You should also see such folders in your BepInEx folder. Copy the DLLs found in those folders in the mod package into their respective folders in your BepInEx directory.

You will also need [DSPModSave v1.1.3](https://dsp.thunderstore.io/package/CommonAPI/DSPModSave/) (the version under the CommonAPI name) or later installed. You can click on "Manual Download" on the linked site to download its package. The package should contain a single DLL, which goes into the `plugins` folder in your BepInEx directory.

### Installation Note
This mod depends on [DSPModSave](https://dsp.thunderstore.io/package/CommonAPI/DSPModSave/). Make sure that you have the version under the CommonAPI name, rather than the old version released by crecheng. The old version has bugs, and may cause problems. The old version of the mod is marked as deprecated.

## Compatibility
This mod is most likely not compatible with the [Nebula Mod](https://dsp.thunderstore.io/package/nebula/NebulaMultiplayerModApi/), however compatibility will be added in the future.

## Planned Optimizations
- [ ] Dyson Node Logic
	- [x] Store CP and SP counts for each layer to avoid recomputing them for each tick
	- [x] Skip checking nodes that aren't being updated on a tick
	- [ ] Change the relevant compute shader to reference a single rotation variable, rather than a copy for each node
- [ ] Dyson spheres
	- [ ] Improve fps by batching shell draw calls
	- [x] Improve fps by updating shader variables only when changed
	- [x] Improve ups by not updating swarm compute shader buffers for hidden layers under construction
- [x] Commands
	- [x] Factory reset command
	- [x] Swarm reset command
	- [x] Sphere reset command
- [ ] Multithreading
	- [ ] Reduce multithreading overhead (currently ~0.11ms of overhead per thread)
- [ ] Power Logic
	- [ ] Fix issues (e.g. stack overflow) with large power networks
	- [ ] Store wind turbine generation to skip recomputing it every tick
	- [ ] Store satellite substation consumption to skip recomputing it every tick
	- [ ] Store solar panel generation on tidally locked planets to skip recomputing it every tick
	- [ ] Create some sort of data structure to keep track of solar panel strength clamping partitions to optimize panels for all planets
	- [ ] Consider adding multithreading to more power logic
	- [ ] Consider changing PowerSystem.RequestDysonSpherePower() to use a smaller pool of RRs instead of the large gen pool
- [x] Storage Logic
	- [x] Add multithreading for belts going in and out of stations
- [ ] Factory Logic
	- [ ] Add multithreading for labs
	- [ ] Add multithreading for splitters
	- [ ] Consider adding multithreading for monitors
- [ ] Belts
	- [ ] Use a new data structure for belts using offsets to minimize cpu time and sorter time
	- [ ] Minimize save file space by taking advantage of the low number of different kinds of items on each belt
	- [ ] Minimize save file space by recomputing rotation and position data
- [ ] Mecha Drone Logic
	- [ ] Consider using the recycle cursor to minimize the number of prebuilds to check
	- [ ] Consider the option of using filters based on what prebuilds are left and what items the player has
	- [ ] Add some spatial data structure to optimize the query for the closest prebuild
	- [ ] Consider adding multithreading
- [ ] Swarm Logic
	- [ ] Optimize sail bullet logic (ejected sails)
	- [ ] Consider adding the option to disable the swarm compute shader if it's only needed for visuals
	- [ ] Consider adding multithreading

## Bug Reports
If you have any bugs or issues to report, then either contact me on discord at Selsion#0769, or raise an issue on this github page.

## Changelog
- v1.1.6
	- added a compatibility fix for the black box mod
	- added factory, swarm, and sphere reset commands
- v1.1.5
	- fixed a bug where selecting other saves in the save menu can reset storage multithreading info
- v1.1.4
	- fixed a bug where multithreading didn't work properly for PLSs. re-enabled storage multithreading
- v1.1.3
	- temporarily disabled the storage multithreading because of potential mod compatibility issues
- v1.1.2
	- added multithreading for station storage logic
- v1.1.1
	- improved fps for dense spheres by optimizing shell rendering
	- reduced lag caused by shader buffers being synced when a hidden layer is under construction
- v1.1.0
	- optimized dyson node logic
- v1.0.6
	- Removed the low resolution shells feature