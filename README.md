# PvPHelper

Tweaks and changes for easy PvP gameplay.

Source repository: [bruglet/PvPHelper](https://github.com/bruglet/PvPHelper).

The first feature keeps a Risk of Rain 2 run active after a full party wipe. The host runs the mod.

On a normal stage, the mod advances to the next destination when every player body is dead. The game then handles the usual stage transition. If the stage has no destination, the mod revives the last player who died. The revived player gets the game's brief extra-life protection.

The mod does not enforce the PvP timer, drone limits, or winner rules.
Team support and skill balance changes are research topics. This version does not implement them.

## Install

The package declares `bbepis-BepInExPack-5.4.2122` as its only external dependency. r2modman does not install dependencies when you import a local ZIP. Install BepInExPack from the Online tab for local testing. A Thunderstore installation can download the declared dependency.

1. Create a separate Risk of Rain 2 profile in r2modman.
2. Install `BepInExPack` in that profile.
3. Remove or disable `PvPStageSaver` if it is installed in this profile.
4. Open **Settings > Profile > Import local mod**.
5. Select `PvPHelper-0.2.0.zip`.
6. Start the game with **Start modded**. The host must use this profile.

## Test

1. Start a private run on a normal stage.
2. Let all players die before the teleporter starts.
3. Make sure that the run moves to the next stage.
4. Make sure that players respawn with their items.
5. Complete a teleporter normally. Make sure that normal stage travel still works.

If the game does not advance, open the BepInEx log in the host's profile folder. Search for `PvPHelper`, `Party wipe`, and `Standard loss was not intercepted`.

## Build from source

Install the .NET SDK. Install `BepInExPack` in an r2modman profile. Then run:

```bash
dotnet build PvPHelper.csproj -c Release \
  -p:GameDir="$HOME/Games/Steam/steamapps/common/Risk of Rain 2" \
  -p:BepInExCoreDir="/path/to/profile/BepInEx/core"
```

The plugin DLL appears in `bin/Release/netstandard2.1/`.

This release builds against Steam game build `21587608` and BepInExPack `5.4.2122`. Rebuild the mod after a game update that changes the RoR2 API.

The stage travel feature passed a solo host wipe test in a multiplayer lobby in version 0.1.1: the log confirmed travel from Golem Plains to Ancient Loft. Version 0.2.0 renames the mod and changes its icon and description; it keeps the same stage travel code. Item retention and a two-player wipe still need testing.

## Development

See [CONTRIBUTING.md](CONTRIBUTING.md) for contribution and test requirements.
See [CHANGELOG.md](CHANGELOG.md) for the release history.

The repository includes source code and package metadata.
Game libraries, build output, and release archives are excluded from Git.
Builds require a local game installation and BepInEx libraries.

The package icon uses [Captain's OGM-72 'DIABLO' Strike artwork](https://riskofrain2.wiki.gg/images/OGM-72_%27DIABLO%27_Strike.png?3031b4=&format=original).
See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for asset attribution.
