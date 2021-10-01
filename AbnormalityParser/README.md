

# AbnormalityParser Mod
This mod can check the reasons why a save was flagged as abnormal and had achievements disabled.

## Installation
You will need to install this mod manually.
### Manual Installation
This mod requires BepInEx to be installed in order to work, so install that first if you haven't already. Download the latest dll for the SwarmDataWipe mod at the [releases](https://github.com/Selsion/DSPMods/releases) page. You will need to put this dll in the `BepInEx\plugins` directory. If you installed BepInEx manually, then this should be found in the game's steam directory. If you're using r2modman, then the path can be found by navigating to the settings page and clicking on "Browse profile folder". Alternatively, the path can be found at `%appdata%\r2modmanPlus-local\DysonSphereProgram\profiles\[PROFILE_NAME]\BepInEx\plugins`, where `[PROFILE_NAME]` is replaced with the name you set when you created the profile.
## Usage
Load a save, then open the [developer console](https://dsp-wiki.com/Developer_Console) and enter the following command:
`-checkMask`

As of version 0.8.22.8915, this command does not disable achievements.

## Bug Reports
If you have any bugs or issues to report, then either contact me on discord at Selsion#0769, or raise an issue on this github page.
