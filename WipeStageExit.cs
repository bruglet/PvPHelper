using RoR2;
using RoR2.Networking;
using UnityEngine.Networking;

namespace PvPHelper
{
    internal static class WipeStageExit
    {
        internal static void Begin(Stage stage, SceneDef destination)
        {
            if (!NetworkServer.active || SceneExitController.isRunning) return;

            SceneExitController exit = stage.gameObject.AddComponent<SceneExitController>();
            exit.destinationScene = destination;
            NetworkPreloadManager.SendScenePreloadMessage(destination);
            // Begin() may skip extraction when keepMoneyBetweenStages is enabled.
            // A wipe must convert all player cash, so enter the native extraction state
            // explicitly. Its converter handles dead masters and native burst rounding;
            // the exit waits for conversion and pending XP before advancing the stage.
            exit.SetState(SceneExitController.ExitState.ExtractExp);
        }
    }
}
