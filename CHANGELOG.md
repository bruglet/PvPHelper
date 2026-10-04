# Changelog

## 0.6.1

- Allowed Antlers shards and vanilla Player-filtered health, ammo, buff, and money pickups to accept every player faction, including their pickup attraction.
- Preserved explicit team filters for Monster Tooth heal packs, Chef food, and other team-specific pickups.
- Expanded Halcyon Shrine's player search to all player factions, restoring eligibility for native gold draining and encounter progression.
- Restored Prayer Beads bonuses, Glass, Chronic Expansion's player-target exclusion, and unlock pickups for custom player teams.
- Changed AI catalog drone → catalog drone damage to DVP (15% default); player → drone and turret ↔ drone still use PVP.
- Added native pickup, shrine, and item-rule hook checks and an audit report. Live verification of these fixes is pending.

## 0.6.0

- Capped each player → player hit at two-thirds of maximum HP plus maximum shields after defenses; remote-controlled drones count as players.
- Suppressed native executions on capped player hits; repeated hits can still kill.
- Added the host-controlled Monster Scaling lobby selector, defaulting to 75%, with 0–100% adjustment in 5-point steps.
- Slowed both elapsed-time growth and the cleared-stage exponent in the native difficulty coefficient and ambient monster level formulas.
- Synchronized the fourth setting with lobby snapshots and retained native starting difficulty, run time, and stage count.
- Extended wire-format, cap, scaling, and native transpiler checks; live verification of these additions is pending.

## 0.5.0

- Added the host-controlled Engi Turret lobby selector, defaulting to 25%.
- Applied Engi Turret to stationary and walking Engineer turret → player damage across colors, including remote-controlled players.
- Applied PVP to player → Engineer turret and Engineer turret ↔ catalog drone damage.
- Retained native damage for Engineer turret ↔ enemy and turret → turret interactions.
- Synchronized the third setting with lobby snapshots and extended damage/serialization checks.

## 0.4.0

- Added host-controlled PVP Damage and DVP Damage arrow selectors in the survivor lobby.
- Defaulted PVP to 50% and DVP to 15%, with 0–200% adjustment in 5-point steps.
- Applied PVP to player → player, player → catalog drone, and catalog drone → catalog drone damage across colors.
- Applied DVP to AI catalog drone → player damage; remote-controlled drones count as players.
- Kept Engineer turrets outside the catalog-only drone classification.
- Synchronized settings with the lobby snapshot and locked edits when launching.
- Scaled damage before defenses without mutating proc inputs or scaling delayed installments again.
- Added damage matrix, settings serialization, and native transpiler checks; live gameplay testing is pending.

## 0.3.0

- Added synchronized Red, Blue, Green, and Yellow player teams.
- Added a native team selector above Ready on the survivor selection screen.
- Kept owned summons on their owner's team and retained choices through respawns.
- Shared vanilla kill money and XP across all player factions.
- Enabled teleporter and player holdout charging across factions, including objectives and Focused Convergence.
- Required R2API Teams and matching installs on every participant.
- Added API and network message checks; live multiplayer testing is pending.

## 0.2.0

- Renamed PvPStageSaver to PvPHelper.
- Changed the description to "Tweaks and changes for easy PvP gameplay".
- Added the Captain Diablo Strike icon.
- Declared BepInExPack as the package dependency.

## 0.1.1

- Intercepted the standard game loss before the run ends.
- Added party wipe diagnostics.
- Passed a solo host wipe test in a multiplayer lobby.

## 0.1.0

- Added stage travel after a full party wipe.
- Added a revival fallback for stages without a destination.
