using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RoR2;

namespace PvPHelper
{
    internal static class PlayerPickups
    {
        // A vanilla Player filter means all player factions. Explicit color filters
        // (for example, Monster Tooth's killer-team heal packs) stay team-specific.
        internal static bool Matches(TeamIndex collector, TeamIndex filter) =>
            collector == filter || (filter == TeamIndex.Player && PlayerTeams.IsPlayerTeam(collector));

        [HarmonyPatch]
        private static class AllowPlayerFactions
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                foreach (Type type in new[] { typeof(ElusiveAntlersPickup), typeof(HealthPickup), typeof(AmmoPickup),
                    typeof(BuffPickup), typeof(MoneyPickup) })
                    yield return type.GetMethod("OnTriggerStay", BindingFlags.Instance | BindingFlags.NonPublic);
                yield return typeof(GravitatePickup).GetMethod("StartGravitate", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var code = new List<CodeInstruction>(instructions);
                MethodInfo getter = typeof(TeamFilter).GetProperty(nameof(TeamFilter.teamIndex)).GetGetMethod();
                MethodInfo matches = typeof(PlayerPickups).GetMethod(nameof(Matches), BindingFlags.Static | BindingFlags.NonPublic);
                int count = 0;
                for (int i = 1; i < code.Count; i++)
                    if (code[i - 1].Calls(getter) && IsEqualityBranch(code[i])) count++;
                if (count != 1) throw new InvalidOperationException("Player pickup filter no longer matches the game API.");

                for (int i = 0; i < code.Count; i++)
                {
                    if (i > 0 && code[i - 1].Calls(getter) && IsEqualityBranch(code[i]))
                    {
                        var call = new CodeInstruction(OpCodes.Call, matches);
                        code[i].MoveLabelsTo(call);
                        code[i].MoveBlocksTo(call);
                        yield return call;
                        yield return new CodeInstruction(code[i].opcode == OpCodes.Beq || code[i].opcode == OpCodes.Beq_S
                            ? OpCodes.Brtrue : OpCodes.Brfalse, code[i].operand);
                    }
                    else yield return code[i];
                }
            }

            private static bool IsEqualityBranch(CodeInstruction instruction) =>
                instruction.opcode == OpCodes.Beq || instruction.opcode == OpCodes.Beq_S ||
                instruction.opcode == OpCodes.Bne_Un || instruction.opcode == OpCodes.Bne_Un_S;
        }
    }
}
