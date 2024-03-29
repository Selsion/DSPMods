﻿# TechUpdater Mod
This mod was made to update tech data from v0.7 to v0.8 for the game Dyson Sphere Program. The two technologies Communication Control and Drone Engine received buffs to some levels, but these changes are not reflected in games where these techs are unlocked before v0.8. This mod fixes that by resetting the three variables for mecha drone count, task count, and drone speed to the starting values, and then calls the tech update function for each level you unlocked. It also fixes the bug where if you completed all 20 levels of Drone Engine before v0.8, then you can't research the new levels up to 24.

## Installation
This mod now has a [Thunderstore release](https://dsp.thunderstore.io/package/Selsion/TechUpdater/). It's recommended that you install it using [r2modman](https://dsp.thunderstore.io/package/ebkr/r2modman/) or another mod manager.
### Manual Installation
This mod requires BepInEx to be installed in order to work. First download the latest dll for the TechUpdater mod at the [releases](https://github.com/Selsion/DSPMods/releases) page. You will need to put this dll in the `BepInEx\plugins` directory. If you installed BepInEx manually, then this should be found in the game's steam directory. If you're using r2modman, then the path can be found by navigating to the settings page and clicking on "Browse profile folder". Alternatively, the path can be found at `%appdata%\r2modmanPlus-local\DysonSphereProgram\profiles\[PROFILE_NAME]\BepInEx\plugins`, where `[PROFILE_NAME]` is replaced with the name you set when you created the profile.
## Usage
The mod will run immediately after a save is loaded, and will recalculate the three tech variables. The updated values will be stored in memory, and will not be saved to disk until you save the game either manually or with autosave. You don't need to leave the mod installed after you save the updated values to disk, and it should be uninstalled afterwards in case of incompatibility with future DSP updates.

It's recommended that you carefully check that the mod behaved as expected before saving, in case of bugs. This means checking that:
 1. The three variables "Construction Drones", "Construction Drone Task Count", and "Construction Drone Flight Speed", seen in the upgrades window, now have values that match your expectations. A spreadsheet containing data on all levels of the techs can be found [here](https://docs.google.com/spreadsheets/d/e/2PACX-1vQkKoADE2gKKOgKJFrUuKe8MmCIcUsyFUcQJxAGGUVNKCuUS4FP3bPSBrUgoCeCSY1JWLaOz7-__n-4/pubhtml#).
 2. Any tech with a new maximum level has been updated correctly. In particular, the infinite tech for Drone Engine has been changed, so verify that the hashes uploaded/needed are correct. Also verify that if you previously finished the tech, that it's no longer finished and you may now research the new levels.
 3. The tech nodes for all levels of Communication Control and Drone Engine appear correct. Only 1 of the techs should have an updated state, as mentioned above, but it's worth checking the others.

If you want to be extra careful, consider saving the game under a new name rather than overwriting the old save.
