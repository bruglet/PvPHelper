# Player-team compatibility audit

Source: native RoR2 assembly from Steam build `21587608`, compared with R2API Teams `1.0.2`. This is a source/IL audit; it does not establish live gameplay results. Version `0.6.1` contains the fixes below.

| Feature | Finding | Fix or retained behavior |
| --- | --- | --- |
| Elusive Antlers shards | `ElusiveAntlersPickup.OnTriggerStay` compares the collector's exact team with its `TeamFilter`. Shard spawning leaves the prefab's Player filter in place. | Player-filtered shards accept vanilla Player plus Red, Blue, Green, and Yellow. Native buff, barrier, sound, ownership, and destruction logic remain. |
| Health, ammo, buff, and money pickups | Each pickup has the same exact-team collection check. `GravitatePickup.StartGravitate` also requires an exact match, so broadening collection alone would leave attraction broken. | Expand a vanilla Player filter to all player factions in collection and attraction. Preserve explicit custom/enemy filters, full-health attraction rules, body flags, and native pickup effects. |
| Monster Tooth | `GlobalEventManager.OnCharacterDeath` assigns the killer's team to the spawned heal pack's synchronized `TeamFilter`. Healing amounts are computed without a vanilla Player restriction. | A colored-team killer's pack already belongs to that color and stays collectible by it. Keep it team-specific; a Player-filtered pack is shared across player factions. No change to healing amounts. |
| Chef food and other dynamically assigned pickups | Chef food, speed/buff pickups, and related kill rewards also assign the source body's team to their filter. | Preserve that team assignment; no blanket rewrite of `TeamFilter` or body teams. |
| Halcyon Shrine | `GoldSiphonNearbyBodyController.SearchForPlayers` adds only vanilla Player to its search mask. Colored players never reach the native gold-drain loop. | Add all four player factions to this search only. Preserve radius, distance order, distinct bodies, player-control requirement, sufficient-money check, target cap, and server-side personal spending. |
| Halcyon encounters | Tier changes and shrine progress depend on stored drain totals. The initial activation director and native encounter code do not have a matching Player-team restriction. | Restore access to the drain/progression path. Do not invent a replacement spawn algorithm. Golems, every tier, rewards, portals, and mixed-team drains require live testing. |
| Prayer Beads | Team XP tracking was shared already, but `CharacterBody.RecalculateStats` applies accumulated health, shield, regeneration, and damage only to vanilla Player. | Recognize all player factions at these four checks. Native bonus values remain unchanged. |
| Artifact of Glass | The same stat method has a separate vanilla Player restriction for Glass. | Recognize all player factions at that check. Preserve native Glass calculations. |
| Chronic Expansion | `GlobalEventManager.ProcessHitEnemy` excludes vanilla Player targets from extending the damage buff's duration. Custom player teams incorrectly count as non-player targets. | Apply the same exclusion to every player faction; do not change buff values or timers. |
| Unlock pickups | `UnlockPickup.OnTriggerStay` requires vanilla Player despite granting the unlock to all participating players. | Recognize all player factions while preserving the native inventory and server checks. |
| Ordinary item/equipment pickups | R2API Teams already patches `GenericPickupController.AttemptGrant` using the team's pickup permission. | Keep that dependency-provided behavior; do not patch it twice. |
| Owned summons and Squid Polyp | Native summon requests carry the summoner; PvPHelper already propagates the owner's color through minion ownership and body spawning. | Retain the existing ownership hooks. |

The compatibility hooks apply locally to these methods. They do not globally map colored teams to Player, change hostility, merge team lists, or change who combat targeting treats as an enemy.

## Remaining non-item checks

The assembly scan also found native Player-team checks in Eclipse's spawn-health, fall-damage, healing, and permanent-damage rules; map boundaries; selected boss/ending sequences and mission member lists; Geode and Blood Siphon searches; and some AI, overlay, and display code. These are follow-up candidates outside this item/shrine patch. They were not verified in game or silently rewritten. The scan matches known team comparison, membership, and mask construction patterns, so it is not an exhaustive compatibility guarantee. Drifter's examined salvage check only chooses a chat message token, rather than blocking the item grant.

## Validation

- Release build: no warnings or errors.
- Resolved 21 feature Harmony targets against the supplied assembly.
- Executed all six pickup/attraction transpilers on native IL; checked positive/negative branch direction, destinations, labels, and unexpected-shape rejection.
- Tested the pickup filter matrix across every native team and all four custom teams. Player filters expand; explicit color/enemy filters remain exact.
- Executed the Halcyon search transpiler on native IL; checked that only its mask-building call changes while native search and branch structure remain.
- Executed the Prayer Beads/Glass, Chronic Expansion, and unlock transpilers on native IL; checked the expected five/one/one Player checks and preserved labels.
- Rechecked the damage matrix, remote-control classification, cap, time/stage scaling, network snapshots, and native damage/difficulty transpilers.

Live acceptance: test Antlers on each color with a host and client; Monster Tooth heal packs from each killer color, attraction, full health, and pickup eligibility; Bandolier ammo packs; buff/money pickups; Prayer Beads removal and retained bonuses; Chronic Expansion on enemies versus player targets; Glass and unlock pickups. For Halcyon, check single and mixed colors, insufficient money, entry/exit from radius, every quality tier, golem encounters, rewards, and portals. Compare drone duels at default and custom DVP values, including 0%, while player/remote-player → drone and turret ↔ drone continue to follow PVP.
