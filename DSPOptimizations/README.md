# DSPOptimizations Mod
This mod adds optimizations to DSP. Few optimizations are available currently, but more will be added in the future.

## Features
### Low Resolution Shells
![low_res_shells_1.gif](https://github.com/Selsion/DSPMods/blob/main/demos/low_res_shells_1.gif?raw=true)

Dyson shells can be made with much fewer vertices than in the vanilla game. This can greatly reduce RAM and save file bloat, as well as help your framerate when a sphere is being rendered. In the dyson sphere editor, when a layer is selected you should see a box appear in the bottom-left side of your screen. This is for configuring the resolution of newly made shells in the selected layer. The button will regenerate all of the shells in the current layer with the given resolution.

The resolution is configured by specifying a sphere radius. Shell geometry will be generated such that the resolution of the geometry will match that of spheres of this radius. For example, if you have a 200km radius sphere layer and set the shell resolution radius of the layer to 4km, then newly made shells will have as few vertices as if you were making a 4km sphere layer. Since the number of vertices in a shell is proportional to its surface area which is proportional to the square of the radius, in this example setting the shell resolution radius to 4km reduces the number of vertices by a factor of 2500.

To aid your choice in setting the shell resolution radius, the expected number of vertices after regenerating the entire layer with the given radius is shown. The memory and save file cost for your shells will be proportional to the vertex count. The current best estimates for these costs are 84 bytes of memory and 42 bytes of save file space per vertex. Setting the value to anything under 10km tends to work well. The minimum value of 1m will result in only 1 vertex per shell, which will hide the shells and contribute almost nothing to RAM and save file costs.

Notes on performance:
- regenerating an entire sphere layer at a very high radius (e.g. 200km) may be slow. Multithreading will be added for this soon
- spheres with a huge number of very tiny shells (e.g. 5000) might hurt your framerate when being rendered because of the high volume of draw calls. This can be fixed by regenerating with a resolution radius of 1m, which causes the draw calls to be skipped.
- making geodesic frames rather than graticule frames might speed up node, frame, and shell creation, as well as shell regeneration

### Other
- Shadows can be disabled in the config

## Installation
This mod has a [Thunderstore release](https://dsp.thunderstore.io/package/Selsion/DSPOptimizations/). It's recommended that you install it with [r2modman](https://dsp.thunderstore.io/package/ebkr/r2modman/) or another mod manager.

### Manual Installation
You will first need to download the mod package, which can be found at the [releases](https://github.com/Selsion/DSPMods/releases) page. This mod requires BepInEx to be installed in order to work, so install that if you haven't already. This mod depends on [BepInEx v5.4.17](https://dsp.thunderstore.io/package/xiaoye97/BepInEx/), but will likely work with earlier versions. If you installed BepInEx manually, then you should see the BepInEx folder in the game's steam directory. If you're using r2modman, then the path can be found by navigating to the settings page and clicking on "Browse profile folder". Alternatively, the path can be found at `%appdata%\r2modmanPlus-local\DysonSphereProgram\profiles\[PROFILE_NAME]\BepInEx`, where `[PROFILE_NAME]` is replaced with the name you set when you created the profile.

Inside the mod package, you should see two folders named `plugins` and `patchers`. You should also see such folders in your BepInEx folder. Copy the DLLs found in those folders in the mod package into their respective folders in your BepInEx directory.

You will also need [DSPModSave v1.1.0](https://dsp.thunderstore.io/package/CommonAPI/DSPModSave/) (the version under the CommonAPI name) installed. You can click on "Manual Download" on the linked site to download its package. The package should contain a single DLL, which goes into the `plugins` folder in your BepInEx directory.

### Installation Note
This mod depends on [DSPModSave](https://dsp.thunderstore.io/package/CommonAPI/DSPModSave/). Make sure that you have the version under the CommonAPI name, rather than the old version released by crecheng. The old version has bugs, and may cause problems. The old version of the mod is marked as deprecated.

## Compatibility
This mod is most likely not compatible with the [Nebula Mod](https://dsp.thunderstore.io/package/nebula/NebulaMultiplayerModApi/), however compatibility will be added in the future.

## Planned Optimizations
- [ ] Low Resolution Shells
	- [x] Core functionality of reducing a shell's resolution
	- [x] UI for choosing the resolution
	- [x] Vanilla CP multithreading
	- [ ] Compute vanilla CP counts instantly with computational geometry
	- [ ] Multithreaded shell regeneration
	- [ ] Integration with SphereEditorTools or upcoming vanilla UI changes
- [ ] Dyson Node Logic
	- [ ] Store CP and SP counts for each layer to avoid recomputing them for each tick
	- [ ] Skip checking nodes that aren't being updated on a tick
	- [ ] Change the relevant compute shader to reference a single rotation variable, rather than a copy for each node
- [ ] Power Logic
	- [ ] Store wind turbine generation to skip recomputing it every tick
	- [ ] Store satellite substation consumption to skip recomputing it every tick
	- [ ] Store solar panel generation on tidally locked planets to skip recomputing it every tick
	- [ ] Create some sort of data structure to keep track of solar panel strength clamping partitions to optimize panels for all planets
	- [ ] Consider adding multithreading to more power logic
	- [ ] Consider changing PowerSystem.RequestDysonSpherePower() to use a smaller pool of RRs instead of the large gen pool
- [ ] Storage Logic
	- [ ] Add multithreading by grouping belts by item when connected to the same station
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
- [ ] Blueprints
	- [ ] Optimize the rendering of planet-wide blueprints
- [ ] Swarm Logic
	- [ ] Optimize sail bullet logic (ejected sails)
	- [ ] Consider adding the option to disable the swarm compute shader if it's only needed for visuals
	- [ ] Consider adding multithreading
- [ ] Save Cleaning
	- [ ] Clean prebuild array after large blueprints are finished to improve permanent drone logic speed

## Bug Reports
If you have any bugs or issues to report, then either contact me on discord at Selsion#0769, or raise an issue on this github page.

## Changelog
- v1.0.5
	- fixed bug where nodes might not queue sails for absorption after importing a modded save
- v1.0.4
	- added multithreading for counting the number of cell points a shell would get in vanilla
- v1.0.3
	- moved the shell resolution panel up to fit on the screen when using a layout height of 900
- v1.0.2
    - initial release on Thunderstore
    - fixed integer overflow bug when filling shell vertices with sails when regenerating
	- added more debug logging and error checking
- v1.0.1
	- fixed issue where when first loading a save with low res shells when the .moddsv file exists from other mods, the low res shells import code isn't called. using the new version of DSPModSave should also fix the issue
- v1.0.0
	- initial pre-release on github
	- implemented low resolution shells
	- added a config option to disable shadows
