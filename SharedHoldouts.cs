using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RoR2;
using RoR2.UI;
using UnityEngine;

namespace PvPHelper
{
    internal static class SharedHoldouts
    {
        [HarmonyPatch(typeof(HoldoutZoneController), "CountLivingPlayers")]
        private static class CountAllPlayers
        {
            private static void Postfix(TeamIndex __0, ref int __result)
            {
                if (__0 != TeamIndex.Player) return;
                foreach (TeamIndex team in PlayerTeams.Teams)
                    foreach (TeamComponent member in TeamComponent.GetTeamMembers(team))
                        if (member.body && member.body.isPlayerControlled && !member.body.isRemoteOp) __result++;
            }
        }

        [HarmonyPatch(typeof(HoldoutZoneController), nameof(HoldoutZoneController.CountPlayersInRadius))]
        private static class CountAllPlayersInRadius
        {
            private static void Postfix(HoldoutZoneController __0, Vector3 __1, float __2, TeamIndex __3, ref int __result)
            {
                if (__3 != TeamIndex.Player) return;
                foreach (TeamIndex team in PlayerTeams.Teams)
                    __result += HoldoutZoneController.CountPlayersInRadius(__0, __1, __2, team);
            }
        }

        [HarmonyPatch(typeof(HoldoutZoneController), "OnCollectObjectiveSources")]
        private static class ShowChargeObjective
        {
            private static readonly System.Type tracker = AccessTools.Inner(typeof(HoldoutZoneController), "ChargeHoldoutZoneObjectiveTracker");
            private static void Postfix(CharacterMaster __0, List<ObjectivePanelController.ObjectiveSourceDescriptor> __1)
            {
                if (!__0 || !PlayerTeams.IsCustom(__0.teamIndex)) return;
                foreach (HoldoutZoneController zone in InstanceTracker.GetInstancesList<HoldoutZoneController>())
                    if (zone.showObjective && zone.chargingTeam == TeamIndex.Player)
                        __1.Add(new ObjectivePanelController.ObjectiveSourceDescriptor { master = __0, objectiveType = tracker, source = zone });
            }
        }

        // Preserve the vanilla Focused Convergence count across the combined charging population.
        // Keep this replacement local to holdout charging; other team item effects stay untouched.
        [HarmonyPatch]
        private static class ShareFocusedConvergence
        {
            private static MethodBase TargetMethod() => AccessTools.Method(
                AccessTools.Inner(typeof(HoldoutZoneController), "FocusConvergenceController"), "DoUpdate");

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                MethodInfo original = AccessTools.Method(typeof(Util), nameof(Util.GetItemCountForTeam),
                    new[] { typeof(TeamIndex), typeof(ItemIndex), typeof(bool), typeof(bool) });
                MethodInfo replacement = AccessTools.Method(typeof(SharedHoldouts), nameof(CountChargingItems));
                int replaced = 0;
                foreach (CodeInstruction instruction in instructions)
                {
                    if (instruction.Calls(original)) { instruction.opcode = OpCodes.Call; instruction.operand = replacement; replaced++; }
                    yield return instruction;
                }
                if (replaced != 1) throw new System.InvalidOperationException("Focused Convergence hook no longer matches the game API.");
            }
        }

        private static int CountChargingItems(TeamIndex team, ItemIndex item, bool requiresAlive, bool requiresConnected)
        {
            if (team != TeamIndex.Player) return Util.GetItemCountForTeam(team, item, requiresAlive, requiresConnected);
            int count = 0;
            foreach (TeamIndex faction in PlayerTeams.RewardTeams)
                count += Util.GetItemCountForTeam(faction, item, requiresAlive, requiresConnected);
            return count;
        }
    }
}
