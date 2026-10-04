using System.Reflection;
using System.Reflection.Emit;
using Mono.Cecil;

internal static class CompatibilityChecks
{
    internal static void Run(Assembly plugin, ModuleDefinition game)
    {
        var team = Assembly.Load("RoR2").GetType("RoR2.TeamIndex")!;
        var teams = (Array)plugin.GetType("PvPHelper.PlayerTeams")!.GetField("Teams", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        for (int i = 0; i < 4; i++) teams.SetValue(Enum.ToObject(team, 5 + i), i);
        var matches = plugin.GetType("PvPHelper.PlayerPickups")!.GetMethod("Matches", BindingFlags.Static | BindingFlags.NonPublic)!;
        for (int collector = -1; collector <= 8; collector++)
            for (int filter = -1; filter <= 8; filter++)
            {
                bool expected = collector == filter || (filter == 1 && (collector == 1 || collector >= 5));
                bool actual = (bool)matches.Invoke(null, new[] { Enum.ToObject(team, collector), Enum.ToObject(team, filter) })!;
                if (actual != expected) throw new Exception($"Pickup filter mismatch: collector {collector}, filter {filter}");
            }
        foreach (string type in new[] { "ElusiveAntlersPickup", "HealthPickup", "AmmoPickup", "BuffPickup", "MoneyPickup", "GravitatePickup" })
        {
            var il = new NativeIl(game, "RoR2." + type, type == "GravitatePickup" ? "StartGravitate" : "OnTriggerStay");
            var input = il.Input.Cast<object>().ToArray();
            var output = il.Apply(plugin, "PlayerPickups+AllowPlayerFactions");
            int[] calls = Enumerable.Range(0, output.Length).Where(i => il.Operand(output[i]) is MethodInfo m &&
                m.DeclaringType?.FullName == "PvPHelper.PlayerPickups" && m.Name == "Matches").ToArray();
            if (calls.Length != 1 || output.Length != input.Length + 1) throw new Exception("Pickup injection mismatch: " + type);
            int call = calls[0];
            var originalBranch = input[call];
            OpCode expectedBranch = il.Opcode(originalBranch) == OpCodes.Beq || il.Opcode(originalBranch) == OpCodes.Beq_S
                ? OpCodes.Brtrue : OpCodes.Brfalse;
            if (il.Opcode(output[call + 1]) != expectedBranch || !Equals(il.Operand(output[call + 1]), il.Operand(originalBranch)))
                throw new Exception("Pickup filter changed branch polarity or destination: " + type);
        }
        Console.WriteLine("PASS pickup filter matrix and all six native pickup/gravity hooks");
        var shrine = new NativeIl(game, "RoR2.GoldSiphonNearbyBodyController", "SearchForPlayers");
        var shrineOpcodes = shrine.Input.Cast<object>().Select(shrine.Opcode).ToArray();
        var shrineOutput = shrine.Apply(plugin, "HalcyonPlayers+FindAllPlayerFactions");
        if (shrineOutput.Length != shrineOpcodes.Length ||
            shrineOutput.Count(i => shrine.Operand(i) is MethodInfo m && m.Name == "AddPlayerTeams" &&
                m.DeclaringType?.FullName == "PvPHelper.HalcyonPlayers") != 1 ||
            shrineOutput.Where((i, index) => shrine.Opcode(i) != shrineOpcodes[index]).Any())
            throw new Exception("Halcyon search replacement changed native control flow");
        foreach (string name in new[] { "FilterCandidatesByHurtBoxTeam", "OrderCandidatesByDistance", "FilterCandidatesByDistinctHurtBoxEntities", "GetHurtBoxes" })
            if (shrineOutput.Count(i => shrine.Operand(i) is MethodInfo m && m.Name == name) != 1)
                throw new Exception("Halcyon native search behavior lost: " + name);
        Console.WriteLine("PASS native Halcyon search mask, radius/search behavior and mismatch rejection");
        var classify = plugin.GetType("PvPHelper.PlayerItemRules")!.GetMethod("ForPlayerCheck", BindingFlags.Static | BindingFlags.NonPublic)!;
        for (int value = -1; value <= 8; value++)
        {
            object actual = classify.Invoke(null, new[] { Enum.ToObject(team, value) })!;
            object expected = Enum.ToObject(team, value >= 5 ? 1 : value);
            if (!Equals(actual, expected)) throw new Exception("Item player classification mismatch: " + value);
        }
        foreach (var (type, method, count) in new[] { ("CharacterBody", "RecalculateStats", 5),
            ("GlobalEventManager", "ProcessHitEnemy", 1), ("UnlockPickup", "OnTriggerStay", 1) })
        {
            var il = new NativeIl(game, "RoR2." + type, method);
            var output = il.Apply(plugin, "PlayerItemRules+RecognizePlayerFactions");
            var calls = Enumerable.Range(0, output.Length).Where(i => il.Operand(output[i]) is MethodInfo m &&
                m.Name == "ForPlayerCheck" && m.DeclaringType?.FullName == "PvPHelper.PlayerItemRules").ToArray();
            if (calls.Length != count || output.Length != il.Input.Count + count)
                throw new Exception("Item hook count mismatch: " + method);
            foreach (int i in calls)
                if (il.Operand(output[i - 1]) is not MethodInfo getter || getter.Name != "get_teamIndex" ||
                    getter.DeclaringType?.FullName != "RoR2.TeamComponent" || il.Opcode(output[i + 1]) != OpCodes.Ldc_I4_1)
                    throw new Exception("Item hook changed a non-player comparison: " + method);
        }
        Console.WriteLine("PASS native Prayer Beads/Glass, Chronic Expansion and unlock player checks");
    }
}
