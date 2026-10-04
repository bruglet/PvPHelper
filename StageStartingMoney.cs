using System;
using System.Collections.Generic;
using HarmonyLib;
using RoR2;
using UnityEngine.Networking;

namespace PvPHelper
{
    internal static class StageStartingMoney
    {
        // The native small-chest price, also used by Longstanding Solitude.
        private const int SmallChestBaseCost = 25;
        private static Stage? fundedStage;
        private static readonly HashSet<CharacterMaster> fundedPlayers = new HashSet<CharacterMaster>();

        internal static void Reset()
        {
            fundedStage = null;
            fundedPlayers.Clear();
        }

        internal static uint GetGrant(int chestCost, uint balance) => chestCost <= 0
            ? 0u
            : (uint)Math.Min((ulong)chestCost * 2u, uint.MaxValue - (ulong)balance);

        [HarmonyPatch(typeof(CharacterMaster), nameof(CharacterMaster.OnBodyStart))]
        private static class FundStageEntry
        {
            private static void Postfix(CharacterMaster __instance)
            {
                if (!NetworkServer.active || !Run.instance) return;
                Stage stage = Stage.instance;
                PlayerCharacterMasterController player = __instance.playerCharacterMasterController;
                if (!stage || !stage.sceneDef || stage.sceneDef.sceneType != SceneType.Stage ||
                    !player || !player.isConnected) return;

                if (!ReferenceEquals(fundedStage, stage))
                {
                    Reset();
                    fundedStage = stage;
                }
                if (!fundedPlayers.Add(__instance)) return;

                // Use the same native cost function as chest generation, with the
                // entry snapshot rather than a later respawn's difficulty. Wait for
                // a body so GiveMoney's native item effects have a valid recipient.
                int chestCost = Run.instance.GetDifficultyScaledCost(SmallChestBaseCost, stage.entryDifficultyCoefficient);
                __instance.GiveMoney(GetGrant(chestCost, __instance.money));
            }
        }
    }
}
