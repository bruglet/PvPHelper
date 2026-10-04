using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class StageMoneyChecks
{
    internal static void Run(Assembly pluginAssembly, ModuleDefinition plugin, ModuleDefinition game)
    {
        void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        int CallIndex(MethodDefinition method, string type, string name) =>
            method.Body.Instructions.ToList().FindIndex(i => i.Operand is MethodReference m && m.DeclaringType.FullName == type && m.Name == name);

        var feature = plugin.Types.Single(t => t.FullName == "PvPHelper.StageStartingMoney");
        var calculate = pluginAssembly.GetType(feature.FullName)!.GetMethod("GetGrant", BindingFlags.NonPublic | BindingFlags.Static)!;
        uint Grant(int price, uint balance) => (uint)calculate.Invoke(null, new object[] { price, balance })!;
        foreach (var example in new[] { (25, 50u), (59, 118u), (141, 282u), (0, 0u), (-1, 0u), (int.MaxValue, uint.MaxValue - 1) })
            Require(Grant(example.Item1, 0u) == example.Item2, "Two-chest grant amount mismatch");
        Require(Grant(59, 200u) == 118u, "Existing legitimate cash must not reduce the grant");
        Require(Grant(59, uint.MaxValue - 30u) == 30u && Grant(59, uint.MaxValue) == 0u,
            "Stage cash must not overflow an existing balance");

        var patch = feature.NestedTypes.Single(t => t.Name == "FundStageEntry").Methods.Single(m => m.Name == "Postfix");
        int server = CallIndex(patch, "UnityEngine.Networking.NetworkServer", "get_active");
        int connected = CallIndex(patch, "RoR2.PlayerCharacterMasterController", "get_isConnected");
        int snapshot = CallIndex(patch, "RoR2.Stage", "get_entryDifficultyCoefficient");
        int priceCall = CallIndex(patch, "RoR2.Run", "GetDifficultyScaledCost");
        int money = CallIndex(patch, "RoR2.CharacterMaster", "GiveMoney");
        Require(server >= 0 && connected > server && snapshot > connected && priceCall > snapshot && money > priceCall,
            "Stage cash must be server-only, player-only and use the entry price before awarding");
        Require(((MethodReference)patch.Body.Instructions[priceCall].Operand).Parameters.Count == 2 &&
            CallIndex(patch, "RoR2.Run", "get_difficultyCoefficient") < 0,
            "A later respawn must not use increased current difficulty");
        Require(patch.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldc_I4_S && (sbyte)i.Operand == 25),
            "Stage cash must use the native $25 small-chest base price");
        Require(patch.Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == "sceneType"),
            "Non-stage scenes must be excluded from funding");
        int add = patch.Body.Instructions.ToList().FindIndex(i => i.Operand is MethodReference m &&
            m.DeclaringType.FullName.StartsWith("System.Collections.Generic.HashSet`1") && m.Name == "Add");
        Require(add >= 0 && add < money && patch.Body.Instructions[add + 1].OpCode.FlowControl == FlowControl.Cond_Branch &&
            CallIndex(patch, feature.FullName, "Reset") >= 0 && CallIndex(patch, feature.FullName, "Reset") < add &&
            patch.Body.Instructions.Take(add).Any(i => i.OpCode == OpCodes.Beq_S || i.OpCode == OpCodes.Beq) &&
            CallIndex(patch, "UnityEngine.Object", "op_Equality") < 0,
            "Repeat bodies must be rejected by the per-stage grant record");
        Require(CallIndex(patch, "PvPHelper.PlayerTeams", "IsPlayerTeam") < 0,
            "Funding must follow player controllers across every faction, not team membership or summons");

        // Check the supplied native lifecycle and cost contract. No pricing formula
        // is duplicated in the plugin; it calls the same method as chest spawning.
        var run = game.Types.Single(t => t.FullName == "RoR2.Run");
        var cost = run.Methods.Single(m => m.Name == "GetDifficultyScaledCost" && m.Parameters.Count == 2);
        Require(cost.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldc_R4 && (float)i.Operand == 1.25f) &&
            cost.Body.Instructions.Any(i => i.OpCode == OpCodes.Conv_I4) && CallIndex(cost, "UnityEngine.Mathf", "Pow") >= 0,
            "Native difficulty-scaled price no longer follows the expected exponent and integer rounding");
        var master = game.Types.Single(t => t.FullName == "RoR2.CharacterMaster");
        var nativeChest = master.Methods.Single(m => m.Name == "OnLevelUpFreeUnlockStageBegin");
        Require(nativeChest.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldc_I4_S && (sbyte)i.Operand == 25),
            "Native small-chest base price changed");
        var stageStart = game.Types.Single(t => t.FullName == "RoR2.Stage").NestedTypes.Single(t => t.Name.StartsWith("<Start>d__"))
            .Methods.Single(m => m.Name == "MoveNext");
        Require(CallIndex(stageStart, "RoR2.Stage", "set_entryDifficultyCoefficient") < CallIndex(stageStart, "RoR2.Stage", "BeginServer") &&
            CallIndex(stageStart, "RoR2.Stage", "BeginServer") < CallIndex(stageStart, "RoR2.Stage", "RespawnLocalPlayers"),
            "Stage entry difficulty and native item setup must precede player respawning");
        Require(CallIndex(master.Methods.Single(m => m.Name == "OnBodyStart"), "RoR2.CharacterBody", "RecalculateStats") >= 0,
            "Native player body must be initialized before the funding postfix");
        Console.WriteLine("PASS stage money: two-chest grants, overflow safety, entry pricing, player scope and once-per-stage guards");
    }
}
