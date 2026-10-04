# PvPHelper

Tweaks and changes for easy PvP gameplay.

Source repository: [bruglet/PvPHelper](https://github.com/bruglet/PvPHelper).

Choose **Red, Blue, Green, or Yellow** using the native **TEAM** button above Ready on the survivor selection screen. Click it to cycle through the colors. Each local player chooses independently; the host validates and synchronizes the choices. Red is the default. Choices apply to the next run and persist through death, respawns, stage travel, and reconnecting to the same session. Teams cannot be changed during a run. Owned summons inherit their owner's team.

The host can also change **PVP Damage**, **DVP Damage**, and **Engi Turret** using the native arrow buttons above TEAM. Each setting runs from **0% to 200%** in **5-point steps**. Everyone sees the same settings, including players joining later. Editing locks when the launch countdown begins, and the selected values apply throughout the run. A new network session starts at PVP **50%**, DVP **15%**, and Engi Turret **25%**.

The host can adjust **Monster Scaling** in the same lobby panel, from **0% to 100%** in **5-point steps**, defaulting to **75%**. It slows both the time contribution and the stage contribution to the native difficulty formula. If the selected fraction is `r`, the formula uses `elapsed time × r` and `1.15^(stages cleared × r)`. At 75%, 20 real minutes count as 15 difficulty minutes, and each cleared stage multiplies difficulty by about 1.11 rather than 1.15. The starting player-count factor and selected difficulty mode stay native. **100%** reproduces vanilla growth; **0%** freezes these two growth inputs at their starting values. The actual run clock and cleared-stage count are unchanged.

This controls the shared difficulty coefficient and ambient monster level together. Consequently, monster levels/stats, director budgets, and difficulty-scaled prices follow the slower growth. It does not reduce all monster stats by a flat 25%. The hook targets the standard `Run.RecalculateDifficultyCoefficentInternal` formula in game build `21587608`; a mod or special run mode that replaces that formula may use its own scaling. Settings synchronize to clients and late joiners and lock for the run like the damage controls.

| Attack between player factions | Setting | Default |
| --- | --- | --- |
| Player → player | PVP Damage | 50% |
| Player → catalog drone | PVP Damage | 50% |
| Catalog drone → catalog drone | DVP Damage | 15% |
| Catalog drone → player | DVP Damage | 15% |
| Engineer turret → player | Engi Turret | 25% |
| Player → Engineer turret | PVP Damage | 50% |
| Engineer turret ↔ catalog drone | PVP Damage | 50% |
| Engineer turret ↔ enemy | Native damage | 100% |

The same rules cover every color pairing. A human-controlled remote-operation drone counts as a **player** on both sides of the table: it deals PVP damage to players, catalog drones, and Engineer turrets; takes DVP damage from AI catalog drones; takes Engi Turret damage from Engineer turrets; and takes PVP damage from players. Drone detection uses only `DroneCatalog`; purchased Gunner Turrets qualify. Engineer's stationary and walking turrets form a separate category identified by `EngiTurretBody` and `EngiWalkerTurretBody`. Engi Turret changes only their attacks against players. Their interactions with catalog drones and incoming player damage follow the PVP setting; turret → turret and enemy interactions retain native damage.

**Each player → player hit is capped at two-thirds of the victim's maximum HP plus maximum shields.** Barrier does not increase this limit and still absorbs damage normally. The cap applies after the PVP multiplier and defenses, including negative armor, crits, and damage-calculation bypass. For example, a victim with 300 maximum combined health can take at most 200 numeric damage from one player hit. It uses maximum health, not current health; subsequent hits, separate proc hits, and DoT ticks can still kill a wounded player. Native executions triggered by those player hits are suppressed so they cannot bypass the cap. Remote-controlled drones count as players on both sides. AI drone and Engineer turret attacks, player attacks against summons, enemy attacks, and self-damage retain their separate existing rules. Delayed damage is capped before splitting into installments; later installments do not multiply the PVP setting again. Direct-death calls outside the damage pipeline and damage with an unidentifiable attacker are outside this hook.

The multiplier applies to calculated attack damage before armor, flat reduction, and protection caps. Critical hits, offensive bonuses, percentage-health attacks, and ordinary damage-over-time ticks use the setting; delayed installments of already-scaled damage do not apply it again. Original hit data used to generate item procs is preserved. **100%** retains the normal numeric damage calculation; **0%** rejects matching damaging hits and suppresses their subsequent normal on-hit processing. Outside capped player → player hits, instant-kill mechanics at nonzero percentages retain native behavior. Direct-death mechanics, monster interactions, self-damage, and hits whose attacker can no longer be identified also retain native behavior. Friendly-fire eligibility also stays native; choosing a percentage does not enable same-team attacks.

Vanilla Player-filtered Antlers shards, health, ammo, buff, and money pickups now accept every player faction, including pickup attraction. Explicit color filters remain team-specific: Monster Tooth and Chef food keep the source body's team. Prayer Beads' accumulated stat bonuses, Glass, Chronic Expansion's player-target exclusion, and unlock pickup eligibility recognize every player faction.

Halcyon Shrine searches every player faction for native gold draining. Its radius, target cap, player-control requirement, costs, personal spending, quality thresholds, and encounter logic remain native. Live testing of golem encounters and the complete shrine flow is still required. See the [compatibility audit](docs/team-compatibility-audit.md) for findings and remaining non-item checks.

Kill money is shared across all player factions using vanilla's global living-player divisor and rounding. Uneven team sizes do not change what each eligible player receives. XP advances one equally progressing pool per faction, preserving vanilla's shared leveling rather than dividing XP by the number of factions. Empty factions also retain progression. Personal money grants and spending retain their normal behavior; dead players retain vanilla reward eligibility.

Players in every faction contribute to player holdout zones, including the teleporter. The charge objective and Focused Convergence count the combined player population. Vanilla radius rules, charge-rate calculations, and the exclusion of remote-operation drones from charging still apply.

The mod also keeps a Risk of Rain 2 run active after a full party wipe.

On a normal stage, the mod advances to the next destination when every player body is dead. The game then handles the usual stage transition. If the stage has no destination, the mod revives the last player who died. The revived player gets the game's brief extra-life protection.

There are no added kill bonuses, AI targeting rules, PvP timers, drone limits, or winner rules. Native hostility between distinct teams applies.

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
6. Verify **PVP Damage**, **DVP Damage**, and **Engi Turret** on mouse and controller, including 0%, 100%, and 200%, rapid edits, Ready/Unready, and launch countdown locking. Also verify **Monster Scaling** at 0%, 75%, and 100%. Only the host can edit; remote clients and late joiners must display all four host values. Start a fresh session and confirm the defaults return.
7. Test the damage table across every opposing color pair with survivors, AI catalog drones, purchased Gunner Turrets, both Engineer turret types, and remote-operation bodies as both attackers and victims. Confirm AI catalog drone → drone follows DVP (15% by default), player → drone follows PVP, and remote-controlled drone → AI drone follows PVP. Check Engineer turret → player at 25% and another configured value, player → turret and turret ↔ drone following PVP, and turret ↔ enemy retaining native damage. Repeat against remote-controlled players. Use matching setups at 100% for comparison, accounting for armor, damage floors, and protection caps.
8. Test melee, bullets, mixed-target explosions, crits, Expose, percentage-health attacks, armor bypass, calculation bypass, DoTs, item proc chains, shields/barrier, and delayed damage. Delayed damage must not be multiplied again; 0% must block matching damaging hits. Recheck the reward and teleporter scenarios after changing damage settings.

9. With 300 maximum combined health and no defenses, test a very large player hit: it should deal at most 200 damage. Test a smaller hit, a lethal follow-up against a wounded player, negative armor, calculation bypass, shields/barrier, DoTs, delayed damage, and native player execution effects. Repeat with remote-controlled drones. Enemy, AI drone, and Engineer turret attacks must retain their existing behavior.
10. Compare otherwise identical runs at Monster Scaling 100%, 75%, and 0%, including stages cleared quickly with little elapsed time and long waits on the first stage. Verify both difficulty coefficient and ambient monster level; 100% should match vanilla and 0% should retain the starting values across time and stage changes. Check that the real clock, stage counter, teleporter, team rewards, and stage travel still work. Repeat with a connected client and a late joiner.

Version 0.5.0 was tested in game and confirmed working by the user. 11. Test Antlers shards, Monster Tooth heal packs, Bandolier ammo packs, buff/money pickups, attraction, Prayer Beads bonuses after removal, Glass, Chronic Expansion on enemies versus players, and unlock pickups for every color. Test Halcyon draining with mixed teams, insufficient money, every tier, golems, rewards, and portals. Use the [audit acceptance checklist](docs/team-compatibility-audit.md) for details.

Version 0.6.1 has passed compilation, API/wire-format checks, damage policy and scaling checks, and execution of both transpilers against supplied native IL. Live gameplay verification of the new cap, scaling control, and compatibility fixes is still pending; the checks above are the acceptance checklist.

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

The .NET 8 verification tool resolves the twenty-one feature Harmony targets against the supplied game assembly, checks the Focused Convergence IL call shape, and round-trips actual UNet requests and snapshots, including damage/scaling settings and percentage boundaries. It tests the actual damage policy for the complete actor matrix, remote-control precedence, exclusions, and valid percentages. It also checks the cap scope and numeric limits, monster scaling defaults and both growth inputs. It executes the compiled damage and difficulty transpilers on the supplied game instructions, checking insertion order, branch-label preservation, and rejection of an unexpected game shape. It also executes the pickup, Halcyon, and item-rule transpilers on native IL and checks pickup-filter eligibility. It does not start Unity or replace gameplay testing:

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
