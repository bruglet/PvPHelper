using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class WipeExitChecks
{
    internal static void Run(ModuleDefinition plugin, ModuleDefinition game)
    {
        MethodDefinition Find(ModuleDefinition module, string type, string method) =>
            module.Types.Single(t => t.FullName == type).Methods.Single(m => m.Name == method);
        int CallIndex(MethodDefinition method, string type, string name) =>
            method.Body.Instructions.ToList().FindIndex(i => i.Operand is MethodReference m && m.DeclaringType.FullName == type && m.Name == name);
        void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

        var begin = Find(plugin, "PvPHelper.WipeStageExit", "Begin");
        int server = CallIndex(begin, "UnityEngine.Networking.NetworkServer", "get_active");
        int running = CallIndex(begin, "RoR2.SceneExitController", "get_isRunning");
        int preload = CallIndex(begin, "RoR2.Networking.NetworkPreloadManager", "SendScenePreloadMessage");
        int extract = CallIndex(begin, "RoR2.SceneExitController", "SetState");
        Require(server >= 0 && running > server && preload > running && extract > preload,
            "Wipe exit must guard server/reentry before starting the native exit");
        var states = game.Types.Single(t => t.FullName == "RoR2.SceneExitController").NestedTypes.Single(t => t.Name == "ExitState");
        Require((int)states.Fields.Single(f => f.Name == "ExtractExp").Constant == 1 &&
            begin.Body.Instructions[extract - 1].OpCode == OpCodes.Ldc_I4_1, "Wipe exit must force native cash extraction");
        Require(CallIndex(begin, "RoR2.Stage", "BeginAdvanceStage") < 0 &&
            CallIndex(begin, "RoR2.TeamManager", "GiveTeamExperience") < 0,
            "Wipe exit must not skip extraction or duplicate native XP awards");

        var continueRun = Find(plugin, "PvPHelper.Plugin", "TryContinueRun");
        Require(CallIndex(continueRun, "RoR2.SceneExitController", "get_isRunning") >= 0 &&
            CallIndex(continueRun, "RoR2.SceneExitController", "get_isRunning") <
            CallIndex(continueRun, "PvPHelper.WipeStageExit", "Begin"), "Repeated loss checks must preserve a running exit");
        Require(CallIndex(continueRun, "RoR2.Stage", "BeginAdvanceStage") < 0, "Wipe still bypasses cash conversion");

        // Check the supplied game's extraction/exit contract rather than reimplementing
        // its cash conversion formula. Body-less masters must still receive team XP.
        var convert = Find(game, "RoR2.ConvertPlayerMoneyToExperience", "FixedUpdate");
        int deduct = CallIndex(convert, "RoR2.CharacterMaster", "set_money");
        Require(deduct >= 0 && CallIndex(convert, "RoR2.ExperienceManager", "AwardExperience") > deduct &&
            CallIndex(convert, "RoR2.TeamManager", "GiveTeamExperience") > deduct,
            "Native converter must clear money and support both body and body-less XP recipients");
        var setState = Find(game, "RoR2.SceneExitController", "SetState");
        Require(setState.Body.Instructions.Any(i => i.Operand is GenericInstanceMethod m && m.Name == "AddComponent" &&
            m.GenericArguments.Single().FullName == "RoR2.ConvertPlayerMoneyToExperience"), "Native exit no longer creates a cash converter");
        var update = Find(game, "RoR2.SceneExitController", "UpdateServer");
        Require(update.Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == "experienceCollector") &&
            CallIndex(update, "RoR2.SceneExitController", "SetState") >= 0 &&
            CallIndex(setState, "RoR2.Stage", "BeginAdvanceStage") >= 0, "Native exit must wait on extraction before stage travel");
        Require(setState.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldc_R4 && (float)i.Operand == 4f),
            "Native exit must retain its XP-delivery/teleport wait");
        Console.WriteLine("PASS wipe exit: native extraction, dead-master XP, travel ordering and reentry guards");
    }
}
