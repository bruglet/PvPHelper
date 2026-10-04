using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RoR2;

namespace PvPHelper
{
    internal static class HalcyonPlayers
    {
        private static void AddPlayerTeams(ref TeamMask mask, TeamIndex requested)
        {
            mask.AddTeam(requested);
            if (requested == TeamIndex.Player)
                foreach (TeamIndex team in PlayerTeams.Teams) mask.AddTeam(team);
        }

        [HarmonyPatch(typeof(GoldSiphonNearbyBodyController), "SearchForPlayers")]
        private static class FindAllPlayerFactions
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var code = new List<CodeInstruction>(instructions);
                MethodInfo add = typeof(TeamMask).GetMethod(nameof(TeamMask.AddTeam));
                MethodInfo allPlayers = typeof(HalcyonPlayers).GetMethod(nameof(AddPlayerTeams), BindingFlags.Static | BindingFlags.NonPublic);
                int matches = 0;
                for (int i = 1; i < code.Count; i++)
                    if (code[i].Calls(add) && code[i - 1].opcode == OpCodes.Ldc_I4_1) matches++;
                if (matches != 1) throw new InvalidOperationException("Halcyon player search no longer matches the game API.");

                for (int i = 0; i < code.Count; i++)
                {
                    if (i > 0 && code[i].Calls(add) && code[i - 1].opcode == OpCodes.Ldc_I4_1)
                    {
                        // Expand only this shrine's Player mask. Keep its radius, ordering,
                        // distinct-body filtering, player-control and money checks native.
                        code[i].opcode = OpCodes.Call;
                        code[i].operand = allPlayers;
                    }
                    yield return code[i];
                }
            }
        }
    }
}
