

# AbnormalityParser Mod
This mod can check the reasons why a save was flagged as abnormal and had achievements disabled. It can also unflag a save as abnormal, however the game will likely flag it as abnormal again.

## Installation
You will need to install this mod manually.
### Manual Installation
This mod requires BepInEx to be installed in order to work, so install that first if you haven't already. Download the latest dll for this mod at the [releases](https://github.com/Selsion/DSPMods/releases) page. You will need to put this dll in the `BepInEx\plugins` directory. If you installed BepInEx manually, then this should be found in the game's steam directory. If you're using r2modman, then the path can be found by navigating to the settings page and clicking on "Browse profile folder". Alternatively, the path can be found at `%appdata%\r2modmanPlus-local\DysonSphereProgram\profiles\[PROFILE_NAME]\BepInEx\plugins`, where `[PROFILE_NAME]` is replaced with the name you set when you created the profile.
## Usage
The following commands are available in-game through the [developer console](https://dsp-wiki.com/Developer_Console):
- `-checkAbnormalities`: prints names for each of the abnormality types detected
- `-dumpAbnormalityInfo [filename]`: logs detailed info about each detected abnormality to the file `AbnormalityParserMod\filename` in your game's document folder. Prints the full path of the saved file. If no filename is given, then `abnormality_info.txt` will be used.
- `-clearAbnormalities`: clears all abnormality data for the save

As of version 0.8.23.9832, these commands do not disable achievements.

The output from the `-dumpAbnormalityInfo` command shows the messages that would have been logged in the game's output log at the time (which may have been overwritten). You may need to translate Chinese text using a tool like google translate or deepl. If you're not sure how to interpret the dumped info, you can contact me on discord at Selsion#0769, and I can take a look at the relevant code in the game to see what exactly happened. If your save was flagged as abnormal on a vanilla save, then it would be appreciated if you send me the outputted file. I may be able to track down the bug causing the issue, and report it to the game's devs.

## Bug Reports
If you have any bugs or issues to report, then either contact me on discord at Selsion#0769, or raise an issue on this github page.
