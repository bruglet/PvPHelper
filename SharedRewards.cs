using HarmonyLib;
using RoR2;
using UnityEngine.Networking;

namespace PvPHelper
{
    internal static class SharedRewards
    {
        private static bool distributingMoney;
        private static bool distributingExperience;

        [HarmonyPatch(typeof(TeamManager), nameof(TeamManager.GiveTeamMoney), new[] { typeof(TeamIndex), typeof(uint) })]
        private static class ShareMoney
        {
            private static bool Prefix(TeamManager __instance, TeamIndex __0, uint __1)
            {
                if (distributingMoney || !NetworkServer.active || !Run.instance || !PlayerTeams.IsPlayerTeam(__0)) return true;
                distributingMoney = true;
                try
                {
                    // Vanilla divides by the global living-player count, not this team's size.
                    // Call once for each disjoint team; each eligible player receives that split once.
                    foreach (TeamIndex team in PlayerTeams.RewardTeams) __instance.GiveTeamMoney(team, __1);
                }
                finally { distributingMoney = false; }
                return false;
            }
        }

        [HarmonyPatch(typeof(TeamManager), nameof(TeamManager.GiveTeamExperience))]
        private static class ShareExperience
        {
            private static bool Prefix(TeamManager __instance, TeamIndex __0, ulong __1)
            {
                if (distributingExperience || !NetworkServer.active || !Run.instance || !PlayerTeams.IsPlayerTeam(__0)) return true;
                distributingExperience = true;
                try
                {
                    // XP is a shared team pool in vanilla, not a per-player division.
                    // Empty factions also advance so joining a faction cannot reset progression.
                    foreach (TeamIndex team in PlayerTeams.RewardTeams)
                    {
                        __instance.GiveTeamExperience(team, __1);
                        if (team == TeamIndex.Player) continue; // Vanilla already handles these beads.
                        foreach (TeamComponent member in TeamComponent.GetTeamMembers(team))
                        {
                            if (member.body && member.body.master) member.body.master.TrackBeadExperience(__1);
                        }
                    }
                }
                finally { distributingExperience = false; }
                return false;
            }
        }
    }
}
