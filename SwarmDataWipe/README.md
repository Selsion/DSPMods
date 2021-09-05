
# SwarmDataWipe Mod
This mod was made to wipe all game data for a swarm. As the sail count for a swarm grows, the game will sometimes double the capacity of various arrays to accommodate the increasing size. These arrays will never be shrunk down to smaller sizes even if your entire swarm is gone, and so you may experience permanent RAM and save file bloat. This is especially bad if you delete dyson shells, which release large amounts of sails at once. As an example, if you have say 6 million sails released at once, then you will get a permanent 312 MB added to your save file. This mod fixes this problem by giving you the ability to wipe such data.

## Installation
This mod does not yet have a ThunderStore release, but it is planned. You will need to install the mod manually for now.
### Manual Installation
This mod requires BepInEx to be installed in order to work, so install that first if you haven't already. Download the latest dll for the SwarmDataWipe mod at the [releases](https://github.com/Selsion/DSPMods/releases) page. You will need to put this dll in the `BepInEx\plugins` directory. If you installed BepInEx manually, then this should be found in the game's steam directory. If you're using r2modman, then the path can be found by navigating to the settings page and clicking on "Browse profile folder". Alternatively, the path can be found at `%appdata%\r2modmanPlus-local\DysonSphereProgram\profiles\[PROFILE_NAME]\BepInEx\plugins`, where `[PROFILE_NAME]` is replaced with the name you set when you created the profile.
## Usage
When in-game, fly to the system with the swarm you wish to delete. You should be close enough that the correct dyson sphere panel can be opened by pressing `Y`. Open the [developer console](https://dsp-wiki.com/Developer_Console) and enter the following command:
`-resetLocalSwarm`

Commands to reset swarms by star index or reset all swarms are not yet implemented, but planned.

## Known Issues
If you run the command while in the UI for an [EM-Rail Ejector](https://dsp-wiki.com/EM-Rail_Ejector), then the buttons for swarm orbits with IDs greater than 1 will still exist, but will be unselectable. You can fix this by reopening the UI.

## Bug Reports
If you have any bugs or issues to report, then either contact me on discord at Selsion#0769, or raise an issue on this github page.
