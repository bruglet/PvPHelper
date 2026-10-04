using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using Mono.Cecil;
using CecilInstruction = Mono.Cecil.Cil.Instruction;

internal static class DamageChecks
{
    internal static void Run(Assembly plugin, ModuleDefinition game)
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        Type policy = plugin.GetType("PvPHelper.DamagePolicy")!;
        Type actor = plugin.GetType("PvPHelper.DamageActor")!;
        MethodInfo classify = policy.GetMethod("Classify", flags)!;
        MethodInfo percent = policy.GetMethod("GetPercent", flags)!;
        MethodInfo valid = policy.GetMethod("IsValidPercent", flags)!;
        object player = Enum.Parse(actor, "Player"), drone = Enum.Parse(actor, "Drone"), other = Enum.Parse(actor, "Other");
        void Equal(object? actual, object expected, string description)
        {
            if (!Equals(actual, expected)) throw new Exception(description + $": expected {expected}, got {actual}");
        }
        int Get(object a, object v, bool teams = true, bool self = false, bool delayed = false, int pvp = 50, int dvp = 15) =>
            (int)percent.Invoke(null, new[] { a, v, teams, self, delayed, pvp, dvp })!;

        Equal(classify.Invoke(null, new object[] { true, false }), player, "Survivor identity");
        Equal(classify.Invoke(null, new object[] { false, true }), drone, "Catalog drone identity");
        object remote = classify.Invoke(null, new object[] { true, true })!;
        Equal(remote, player, "Remote control must win over drone identity");
        Equal(classify.Invoke(null, new object[] { false, false }), other, "Engineer turret/other body stays outside catalog rules");
        // The expected matrix is the requested behavior, independent of the implementation.
        object[] actors = { player, drone, other };
        int[,] expected = { { 50, 50, 100 }, { 15, 50, 100 }, { 100, 100, 100 } };
        for (int a = 0; a < actors.Length; a++)
            for (int v = 0; v < actors.Length; v++) Equal(Get(actors[a], actors[v]), expected[a, v], $"{actors[a]} -> {actors[v]}");
        Equal(Get(remote, player), 50, "Remote drone outgoing PVP");
        Equal(Get(drone, remote), 15, "Remote drone incoming DVP");
        Equal(Get(remote, drone), 50, "Remote drone -> AI drone PVP");
        Equal(Get(remote, remote), 50, "Remote -> remote PVP");
        Equal(Get(drone, drone, pvp: 85, dvp: 35), 85, "Drone -> drone follows configured PVP");
        Equal(Get(drone, player, pvp: 85, dvp: 35), 35, "Drone -> player follows configured DVP");
        Equal(Get(player, drone, pvp: 85, dvp: 35), 85, "Player -> drone follows configured PVP");
        Equal(Get(player, player, teams: false), 100, "Non-player factions");
        Equal(Get(drone, player, teams: false), 100, "Monster drone damage");
        Equal(Get(player, player, self: true), 100, "Self damage");
        Equal(Get(drone, player, delayed: true), 100, "Delayed installments must not scale twice");
        Equal(Get(player, player, pvp: 0), 0, "PVP off");
        Equal(Get(drone, player, dvp: 0), 0, "DVP off");
        Equal(Get(player, player, pvp: 200), 200, "PVP maximum");
        foreach (int value in new[] { 0, 15, 50, 100, 200 }) Equal(valid.Invoke(null, new object[] { value }), true, "Valid percentage");
        foreach (int value in new[] { -5, 1, 201, 205, 65535 }) Equal(valid.Invoke(null, new object[] { value }), false, "Invalid percentage");
        Console.WriteLine("PASS damage matrix, remote-control precedence, exclusions and percentage bounds");

        VerifyTranspiler(plugin, game);
    }

    private static void VerifyTranspiler(Assembly plugin, ModuleDefinition game)
    {
        // Feed the real compiled transpiler real game instructions. No Unity objects or
        // Harmony patch application is needed, so this can run outside the game on .NET 8.
        Type patch = plugin.GetType("PvPHelper.DamageScaling+ScaleCalculatedDamage")!;
        Type instructionType = Assembly.Load("0Harmony").GetType("HarmonyLib.CodeInstruction")!;
        var input = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(instructionType))!;
        var method = game.Types.Single(t => t.FullName == "RoR2.HealthComponent").Methods.Single(m => m.Name == "TakeDamageProcess");
        MethodInfo runtimeMethod = Assembly.Load("RoR2").GetType("RoR2.HealthComponent")!
            .GetMethod("TakeDamageProcess", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Module module = runtimeMethod.Module;
        var generator = new DynamicMethod("DamageHookCheck", typeof(void), Type.EmptyTypes).GetILGenerator();
        var locals = runtimeMethod.GetMethodBody()!.LocalVariables.Select(l => generator.DeclareLocal(l.LocalType, l.IsPinned)).ToArray();
        var labels = method.Body.Instructions.ToDictionary(i => i, _ => generator.DefineLabel());
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => o.Name!);
        FieldInfo labelField = instructionType.GetField("labels")!;
        FieldInfo opcodeField = instructionType.GetField("opcode")!;
        FieldInfo operandField = instructionType.GetField("operand")!;
        foreach (CecilInstruction instruction in method.Body.Instructions)
        {
            object? operand = instruction.Operand switch
            {
                FieldReference field => module.ResolveField(field.MetadataToken.ToInt32()),
                MethodReference call => module.ResolveMethod(call.MetadataToken.ToInt32()),
                TypeReference type => module.ResolveType(type.MetadataToken.ToInt32()),
                Mono.Cecil.Cil.VariableDefinition local => locals[local.Index],
                ParameterDefinition parameter => (short)(parameter.Index + 1),
                CecilInstruction target => labels[target],
                CecilInstruction[] targets => targets.Select(t => labels[t]).ToArray(),
                _ => instruction.Operand
            };
            object code = Activator.CreateInstance(instructionType, opcodes[instruction.OpCode.Name], operand)!;
            ((IList)labelField.GetValue(code)!).Add(labels[instruction]);
            input.Add(code);
        }
        var transpile = patch.GetMethod("Transpiler", BindingFlags.NonPublic | BindingFlags.Static)!;
        var output = ((IEnumerable)transpile.Invoke(null, new object[] { input })!).Cast<object>().ToArray();
        int FindCall(string name) => Array.FindIndex(output, i => operandField.GetValue(i) is MethodInfo called &&
            called.DeclaringType?.FullName == "PvPHelper.DamageScaling" && called.Name == name);
        int normal = FindCall("ScaleDamage"), bypass = FindCall("ScaleBypassDamage");
        int armor = Array.FindIndex(output, i => operandField.GetValue(i) is MethodInfo called &&
            called.DeclaringType?.FullName == "RoR2.CharacterBody" && called.Name == "get_armor");
        if (bypass < 0 || normal <= bypass || armor <= normal || output.Length != input.Count + 10)
            throw new Exception("Damage injection count or ordering mismatch");
        if (output.Count(i => operandField.GetValue(i) is MethodInfo called &&
            called.DeclaringType?.FullName == "PvPHelper.DamageScaling") != 2)
            throw new Exception("Damage scale call duplicated");
        if (output.Count(i => (OpCode)opcodeField.GetValue(i)! == OpCodes.Stfld && operandField.GetValue(i) is FieldInfo field &&
            field.DeclaringType?.FullName == "RoR2.DamageInfo" && field.Name == "damage") !=
            input.Cast<object>().Count(i => (OpCode)opcodeField.GetValue(i)! == OpCodes.Stfld && operandField.GetValue(i) is FieldInfo field &&
            field.DeclaringType?.FullName == "RoR2.DamageInfo" && field.Name == "damage"))
            throw new Exception("Unexpected damage-info mutation");
        var originalLabels = labels.Values.ToArray();
        var outputLabels = output.SelectMany(i => ((IList)labelField.GetValue(i)!).Cast<Label>()).ToArray();
        if (outputLabels.Length != originalLabels.Length || originalLabels.Any(l => outputLabels.Count(o => o.Equals(l)) != 1))
            throw new Exception("Branch labels were lost or duplicated");
        // A changed game shape must be rejected rather than silently patching the wrong local.
        var empty = Activator.CreateInstance(typeof(List<>).MakeGenericType(instructionType))!;
        try
        {
            ((IEnumerable)transpile.Invoke(null, new[] { empty })!).Cast<object>().ToArray();
            throw new Exception("Unexpected game shape was accepted");
        }
        catch (InvalidOperationException) { }
        Console.WriteLine("PASS actual damage transpiler: native IL, calculation order, label preservation and mismatch rejection");
    }
}
