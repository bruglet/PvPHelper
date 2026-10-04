using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RoR2;

namespace PvPHelper
{
    internal static class PlayerItemRules
    {
        private static TeamIndex ForPlayerCheck(TeamIndex team) => PlayerTeams.IsPlayerTeam(team) ? TeamIndex.Player : team;

        [HarmonyPatch]
        private static class RecognizePlayerFactions
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                yield return typeof(CharacterBody).GetMethod(nameof(CharacterBody.RecalculateStats), flags);
                yield return typeof(GlobalEventManager).GetMethod("ProcessHitEnemy", flags);
                yield return typeof(UnlockPickup).GetMethod("OnTriggerStay", flags);
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
            {
                var code = new List<CodeInstruction>(instructions);
                MethodInfo getter = typeof(TeamComponent).GetProperty(nameof(TeamComponent.teamIndex)).GetGetMethod();
                MethodInfo playerCheck = typeof(PlayerItemRules).GetMethod(nameof(ForPlayerCheck), BindingFlags.Static | BindingFlags.NonPublic);
                int count = 0;
                for (int i = 0; i < code.Count - 2; i++) if (IsPlayerCheck(code, i, getter)) count++;
                // RecalculateStats: Glass plus four Prayer Beads bonuses. ProcessHitEnemy:
                // Chronic Expansion must not extend its buff by hitting a player faction.
                int expected = __originalMethod.DeclaringType == typeof(CharacterBody) ? 5 : 1;
                if (count != expected) throw new InvalidOperationException("Player item checks no longer match the game API: " + __originalMethod.Name);

                for (int i = 0; i < code.Count; i++)
                {
                    yield return code[i];
                    if (IsPlayerCheck(code, i, getter)) yield return new CodeInstruction(OpCodes.Call, playerCheck);
                }
            }

            private static bool IsPlayerCheck(List<CodeInstruction> code, int i, MethodInfo getter) =>
                i + 2 < code.Count && code[i].Calls(getter) && code[i + 1].opcode == OpCodes.Ldc_I4_1 &&
                (code[i + 2].opcode == OpCodes.Beq || code[i + 2].opcode == OpCodes.Beq_S ||
                 code[i + 2].opcode == OpCodes.Bne_Un || code[i + 2].opcode == OpCodes.Bne_Un_S);
        }
    }
}
