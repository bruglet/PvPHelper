using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using RoR2;
using UnityEngine.Networking;

namespace PvPHelper
{
    [BepInPlugin(Guid, "PvPHelper", "0.2.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.brug.pvphelper";

        private static CharacterMaster? lastPlayerToDie;
        private static ManualLogSource? log;
        private Harmony? harmony;

        private void Awake()
        {
            log = Logger;
            harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(Plugin).Assembly);
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }

        [HarmonyPatch(typeof(CharacterMaster), nameof(CharacterMaster.OnBodyDeath), new[] { typeof(CharacterBody) })]
        private static class RememberLastPlayerDeath
        {
            private static void Postfix(CharacterMaster __instance)
            {
                if (!NetworkServer.active || !__instance.playerCharacterMasterController)
                    return;

                lastPlayerToDie = __instance;
                if (Run.instance)
                    TryContinueRun(Run.instance, false);
            }
        }

        [HarmonyPatch(typeof(Run), nameof(Run.BeginGameOver))]
        private static class ContinueAfterPartyWipe
        {
            private static bool Prefix(Run __instance, GameEndingDef __0)
            {
                if (__0 != RoR2Content.GameEndings.StandardLoss)
                    return true;

                if (TryContinueRun(__instance, true))
                    return false;

                Stage stage = Stage.instance;
                SceneDef scene = SceneCatalog.GetSceneDefForCurrentScene();
                log?.LogWarning($"Standard loss was not intercepted: server={NetworkServer.active}, " +
                    $"gameOver={__instance.isGameOverServer}, scene={scene?.cachedName}, " +
                    $"stageReady={(stage && stage.stageAdvanceTime.isInfinity)}, " +
                    $"allDefeated={AllConnectedPlayersDefeated()}, " +
                    $"players={__instance.participatingPlayerCount}, livingBodies={__instance.livingPlayerCount}.");
                return true;
            }
        }

        private static bool TryContinueRun(Run run, bool allowRevive)
        {
            if (!NetworkServer.active || !run || run.isGameOverServer || !AllConnectedPlayersDefeated())
                return false;

            Stage stage = Stage.instance;
            SceneDef scene = SceneCatalog.GetSceneDefForCurrentScene();
            if (!stage || !scene || scene.sceneType != SceneType.Stage ||
                !stage.stageAdvanceTime.isInfinity)
                return false;

            if (!scene.isFinalStage)
            {
                if (!run.nextStageScene)
                    run.PickNextStageSceneFromCurrentSceneDestinations();

                SceneDef next = run.nextStageScene;
                if (next)
                {
                    stage.BeginAdvanceStage(next);
                    log?.LogInfo($"Party wipe: advancing from {scene.cachedName} to {next.cachedName}.");
                    return true;
                }
            }

            if (!allowRevive)
                return false;

            CharacterMaster? master = GetPlayerToRevive();
            if (master is null || !master)
                return false;

            master.RespawnExtraLife();
            CharacterBody body = master.GetBody();
            if (!body || !body.healthComponent || !body.healthComponent.alive)
                return false;

            log?.LogInfo("Party wipe: revived the last player to die because this stage has no destination.");
            return true;
        }

        private static bool AllConnectedPlayersDefeated()
        {
            int connected = 0;
            foreach (PlayerCharacterMasterController player in PlayerCharacterMasterController.instances)
            {
                if (!player || !player.isConnected || !player.master)
                    continue;

                connected++;
                if (player.preventGameOver)
                    return false;
            }

            return connected > 0;
        }

        private static CharacterMaster? GetPlayerToRevive()
        {
            if (CanRevive(lastPlayerToDie))
                return lastPlayerToDie;

            foreach (PlayerCharacterMasterController player in PlayerCharacterMasterController.instances)
            {
                if (player && player.isConnected && CanRevive(player.master))
                    return player.master;
            }

            return null;
        }

        private static bool CanRevive(CharacterMaster? master)
        {
            if (master is null || !master)
                return false;

            PlayerCharacterMasterController player = master.playerCharacterMasterController;
            if (!player || !player.isConnected)
                return false;

            CharacterBody body = master.GetBody();
            return !body || !body.healthComponent || !body.healthComponent.alive;
        }
    }
}
