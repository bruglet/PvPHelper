# PvPHelper

Tweaks and changes for easy PvP gameplay.

Source repository: [bruglet/PvPHelper](https://github.com/bruglet/PvPHelper).

Choose **Red, Blue, Green, or Yellow** using the native **TEAM** button above Ready on the survivor selection screen. Click it to cycle through the colors. Each local player chooses independently; the host validates and synchronizes the choices. Red is the default. Choices apply to the next run and persist through death, respawns, stage travel, and reconnecting to the same session. Teams cannot be changed during a run. Owned summons inherit their owner's team.

Kill money is shared across all player factions using vanilla's global living-player divisor and rounding. Uneven team sizes do not change what each eligible player receives. XP advances one equally progressing pool per faction, preserving vanilla's shared leveling rather than dividing XP by the number of factions. Empty factions also retain progression. Personal money grants and spending retain their normal behavior; dead players retain vanilla reward eligibility.

Players in every faction contribute to player holdout zones, including the teleporter. The charge objective and Focused Convergence count the combined player population. Vanilla radius rules, charge-rate calculations, and the exclusion of remote-operation drones from charging still apply.

The mod also keeps a Risk of Rain 2 run active after a full party wipe.

On a normal stage, the mod advances to the next destination when every player body is dead. The game then handles the usual stage transition. If the stage has no destination, the mod revives the last player who died. The revived player gets the game's brief extra-life protection.

There are no added damage multipliers, stat scaling, kill bonuses, AI targeting rules, PvP timers, drone limits, or winner rules. Native hostility between distinct teams applies.

## Install

**Every participant, including the host, must install this version and its dependencies.** Team support is no longer host-only. The mod participates in the game's mod compatibility check.

The package declares `bbepis-BepInExPack-5.4.2122` and `RiskofThunder-R2API_Teams-1.0.2`. R2API Teams includes a preloader patcher; install its complete package and its transitive dependencies, not just its plugin DLL. r2modman does not install dependencies when importing a local ZIP. Install them from the Online tab first.

1. Create a separate Risk of Rain 2 profile in r2modman.
2. Install `BepInExPack` and `R2API_Teams` with their dependencies in that profile.
3. Remove or disable `PvPStageSaver` if it is installed in this profile.
4. Open **Settings > Profile > Import local mod**.
5. Import a package built from this branch, or copy `PvPHelper.dll` into the profile's `BepInEx/plugins/PvPHelper` folder.
6. Start the game with **Start modded** on every participant's machine.

## Test

Use a separate profile. Record the game build, mod versions, and host/client logs.

1. In a solo lobby and a multiplayer lobby, cycle through all four choices with mouse and controller. Confirm selecting a team does not Ready the player. Test rapid clicks and local split-screen users. Remote players must only change their own choices.
2. Start a run with players on different colors. Check master/body teams, same-team friendliness, native opposing-team hostility, owned drones/turrets, stage respawns, and reconnecting.
3. Test uneven team sizes (for example, two Red and one Blue). Kill a monster with each faction and an owned summon. Each vanilla-eligible player should receive the same rounded vanilla share of money once, regardless of faction. All factions should gain the same vanilla XP and levels. Check Prayer Beads, XP orbs, death, and stage-end money conversion. Compare against an otherwise matching vanilla run.
4. Activate a teleporter from each color. Move different combinations into and out of its radius and check normal charging, the HUD objective, and Focused Convergence. Verify remote-operation drones do not charge, matching vanilla. Check another player holdout zone as well.
5. Let all players die before the teleporter starts. Confirm stage travel, respawning with items and the selected teams, and the final-stage revival fallback. Complete a teleporter normally and confirm normal travel.

Version 0.3.0 has passed compilation and the API/wire-format checks below. Live UI and multiplayer gameplay verification is still pending; the checks above are the acceptance checklist.

If the game does not advance, open the BepInEx log in the host's profile folder. Search for `PvPHelper`, `Party wipe`, and `Standard loss was not intercepted`.

## Build from source

Install the .NET SDK, `BepInExPack`, and `R2API_Teams` in an r2modman profile. Point `R2APITeamsDir` at the directory containing `R2API.Teams.dll`. Then run:

```bash
dotnet build PvPHelper.csproj -c Release \
  -p:GameDir="$HOME/Games/Steam/steamapps/common/Risk of Rain 2" \
  -p:BepInExCoreDir="/path/to/profile/BepInEx/core" \
  -p:R2APITeamsDir="/path/to/profile/BepInEx/plugins/RiskofThunder-R2API_Teams"
```

The plugin DLL appears in `bin/Release/netstandard2.1/`.

This release builds against Steam game build `21587608` and BepInExPack `5.4.2122`. Rebuild the mod after a game update that changes the RoR2 API.

The .NET 8 verification tool resolves the new Harmony targets against the supplied game assembly, checks the Focused Convergence IL call shape, and round-trips actual UNet requests (four colors and the query sentinel), populated snapshots, and empty snapshots. It does not start Unity or replace gameplay testing:

```bash
dotnet run --project tools/VerifyApi \
  -p:BepInExCoreDir="/path/to/profile/BepInEx/core" -- \
  "$PWD/bin/Release/netstandard2.1/PvPHelper.dll" \
  "/path/to/game/Risk of Rain 2_Data/Managed" \
  "/path/to/profile/BepInEx/core" \
  "/path/to/directory/containing/R2API.Teams.dll"
```

The stage travel feature passed a solo host wipe test in a multiplayer lobby in version 0.1.1: the log confirmed travel from Golem Plains to Ancient Loft. Version 0.2.0 renames the mod and changes its icon and description; it keeps the same stage travel code. Item retention and a two-player wipe still need testing.

## Development

See [CONTRIBUTING.md](CONTRIBUTING.md) for contribution and test requirements.
See [CHANGELOG.md](CHANGELOG.md) for the release history.

The repository includes source code and package metadata.
Game libraries, build output, and release archives are excluded from Git.
Builds require a local game installation and BepInEx libraries.

The package icon uses [Captain's OGM-72 'DIABLO' Strike artwork](https://riskofrain2.wiki.gg/images/OGM-72_%27DIABLO%27_Strike.png?3031b4=&format=original).
See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for asset attribution.
