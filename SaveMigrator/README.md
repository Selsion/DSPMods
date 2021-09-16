
# SaveMigrator Mod
This mod was made to update save data after new updates were not reflected in old saves. Currently, it supports updating gas planets to their new types. You can use it to get the new gas giant type, which would otherwise require making a new game.

## Installation
This mod does not yet have a Thunderstore release, but it will be added once more people have tested it and confirmed that it's working correctly.
### Manual Installation
This mod requires BepInEx to be installed in order to work, so install that first if you haven't already. Download the latest dll for the SaveMigrator mod at the [releases](https://github.com/Selsion/DSPMods/releases) page. You will need to put this dll in the `BepInEx\plugins` directory. If you installed BepInEx manually, then this should be found in the game's steam directory. If you're using r2modman, then the path can be found by navigating to the settings page in r2modman and clicking on "Browse profile folder". Alternatively, the path can be found at `%appdata%\r2modmanPlus-local\DysonSphereProgram\profiles\[PROFILE_NAME]\BepInEx\plugins`, where `[PROFILE_NAME]` is replaced with the name you set when you created the profile.
## Usage
The mod will run immediately after a save is loaded, and will regenerate the planet types for each gas planet. The updated data will be stored in memory, and will not be saved to disk until you save the game either manually or with autosave. You don't need to leave the mod installed after you save the updated data to disk, and it should be uninstalled afterwards in case of incompatibility with future DSP updates.

It's recommended that you carefully check that the mod behaved as expected before saving, in case of bugs. This means checking that:
 1. All gas planets have been changed to the new expected type
 2. The appearances for the gas planets are correct (e.g. no planet has the appearance of a different type)

If you want to be extra careful, consider saving the game under a new name rather than overwriting the old save.
