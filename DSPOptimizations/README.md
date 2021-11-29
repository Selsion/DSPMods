


# DSPOptimizations Mod
This mod adds optimizations to DSP. Few optimizations are available currently, but more will be added in the future.

## Features
### Low Resolution Shells
![Low Resolution Shells](https://github.com/Selsion/DSPMods/blob/main/demos/low_res_shells_1.gif)

Dyson shells can be made with much fewer vertices than in the vanilla game. This can greatly reduce RAM and save file bloat, as well as help your framerate when a sphere is being rendered. In the dyson sphere editor, when a layer is selected you should see a box appear in the bottom-left side of your screen. This is for configuring the resolution of newly made shells in the selected layer. The button will regenerate all of the shells in the current layer with the given resolution.

The resolution is configured by specifying a sphere radius. Shell geometry will be generated such that the resolution of the geometry will match that of spheres of this radius. For example, if you have a 200km radius sphere layer and set the shell resolution radius of the layer to 4km, then newly made shells will have as few vertices as if you were making a 4km sphere layer. Since the number of vertices in a shell is proportional to its surface area which is proportional to the square of the radius, in this example setting the shell resolution radius to 4km reduces the number of vertices by a factor of 2500.

To aid your choice in setting the shell resolution radius, the expected number of vertices after regenerating the entire layer with the given radius is shown. The memory and save file cost for your shells will be proportional to the vertex count. The current best estimates for these costs are 84 bytes of memory and 42 bytes of save file space per vertex.
### Other
- Shadows can be disabled in the config

## Installation
This mod currently has only a pre-release published here on this github repo. A full release will soon be published to Thunderstore.

It's recommended that you use r2modman to install the mod. You will first need to download the mod package, which can be found at the [releases](https://github.com/Selsion/DSPMods/releases) page. After doing so, follow the instructions below for your preferred installation method.
### Installation with r2modman
Browse to the settings page in r2modman. Look for "Import local mod", which is most easily found using the search bar. You can also find it under the "Profile" filter. Select the zip file that you downloaded. At the time of writing, the zip file should be named `Selsion-DSPOptimizations-1.0.0.zip`. This should open a window where you can set the mod info. This should have been loaded from the manifest file, so you can leave the info as-is. This mod depends on [BepInEx v5.4.17](https://dsp.thunderstore.io/package/xiaoye97/BepInEx/) and [DSPModSave v1.0.2](https://dsp.thunderstore.io/package/crecheng/DSPModSave/), so download or update those mods if needed.
### Manual Installation
This mod requires BepInEx to be installed in order to work, so install that first if you haven't already. This mod depends on [BepInEx v5.4.17](https://dsp.thunderstore.io/package/xiaoye97/BepInEx/), but will likely work with earlier versions. If you installed BepInEx manually, then you should see the BepInEx folder in the game's steam directory. If you're using r2modman, then the path can be found by navigating to the settings page and clicking on "Browse profile folder". Alternatively, the path can be found at `%appdata%\r2modmanPlus-local\DysonSphereProgram\profiles\[PROFILE_NAME]\BepInEx`, where `[PROFILE_NAME]` is replaced with the name you set when you created the profile.

Inside the mod package, you should see two folders named `plugins` and `patchers`. You should also see such folders in your BepInEx folder. Copy the DLLs found in those folders in the mod package into their respective folders in your BepInEx directory.

You will also need [DSPModSave v1.0.2](https://dsp.thunderstore.io/package/crecheng/DSPModSave/) installed. You can click on "Manual Download" on the linked site to download its package. The package should contain a single DLL, which goes into the `plugins` folder in your BepInEx directory.

## Bug Reports
If you have any bugs or issues to report, then either contact me on discord at Selsion#0769, or raise an issue on this github page.

## Changelog
- v1.0.0
	- initial pre-release on github
	- implemented low resolution shells
	- added a config option to disable shadows
