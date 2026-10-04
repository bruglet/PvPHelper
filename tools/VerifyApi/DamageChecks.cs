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
        object player = Enum.Parse(actor, "Player"), drone = Enum.Parse(actor, "Drone"),
            turret = Enum.Parse(actor, "EngineerTurret"), other = Enum.Parse(actor, "Other");
        void Equal(object? actual, object expected, string description)
        {
            if (!Equals(actual, expected)) throw new Exception(description + $": expected {expected}, got {actual}");
        }
        int Get(object a, object v, bool teams = true, bool self = false, bool delayed = false, int pvp = 50, int dvp = 15, int engi = 25) =>
            (int)percent.Invoke(null, new[] { a, v, teams, self, delayed, pvp, dvp, engi })!;

        Equal(classify.Invoke(null, new object[] { true, false, false }), player, "Survivor identity");
        Equal(classify.Invoke(null, new object[] { false, true, false }), drone, "Catalog drone identity");
        Equal(classify.Invoke(null, new object[] { false, false, true }), turret, "Engineer turret identity");
        Equal(classify.Invoke(null, new object[] { false, true, true }), turret, "Engineer turret wins over catalog overlap");
        Equal(classify.Invoke(null, new object[] { true, true, true }), player, "Human control wins over turret identity");
        object remote = classify.Invoke(null, new object[] { true, true, false })!;
        Equal(remote, player, "Remote control must win over drone identity");
        Equal(classify.Invoke(null, new object[] { false, false, false }), other, "Other bodies stay outside the rules");
        // The expected matrix is the requested behavior, independent of the implementation.
        object[] actors = { player, drone, turret, other };
        int[,] expected = { { 50, 50, 50, 100 }, { 15, 50, 50, 100 }, { 25, 50, 100, 100 }, { 100, 100, 100, 100 } };
        for (int a = 0; a < actors.Length; a++)
            for (int v = 0; v < actors.Length; v++) Equal(Get(actors[a], actors[v]), expected[a, v], $"{actors[a]} -> {actors[v]}");
        Equal(Get(remote, player), 50, "Remote drone outgoing PVP");
        Equal(Get(drone, remote), 15, "Remote drone incoming DVP");
        Equal(Get(remote, drone), 50, "Remote drone -> AI drone PVP");
        Equal(Get(remote, remote), 50, "Remote -> remote PVP");
        Equal(Get(turret, remote), 25, "Turret -> remote player uses Engi Turret");
        Equal(Get(remote, turret), 50, "Remote player -> turret uses PVP");
        Equal(Get(turret, player, pvp: 85, dvp: 35, engi: 40), 40, "Turret -> player follows configured Engi Turret");
        Equal(Get(player, turret, pvp: 85, engi: 40), 85, "Player -> turret follows PVP");
        Equal(Get(drone, turret, pvp: 85, dvp: 35, engi: 40), 85, "Drone -> turret follows PVP");
        Equal(Get(turret, drone, pvp: 85, engi: 40), 85, "Turret -> drone follows PVP");
        Equal(Get(turret, turret, pvp: 85, engi: 40), 100, "Turret -> turret retains native damage");
        Equal(Get(turret, other, engi: 0), 100, "Turret -> enemy retains native damage");
        Equal(Get(other, turret, pvp: 0, engi: 0), 100, "Enemy -> turret retains native damage");
        Equal(Get(turret, player, teams: false, engi: 0), 100, "Enemy-team turret -> player retains native damage");
        Equal(Get(player, turret, teams: false, pvp: 0), 100, "Player -> enemy-team turret retains native damage");
        Equal(Get(turret, player, delayed: true, engi: 0), 100, "Turret delayed installments must not scale twice");
        Equal(Get(turret, player, engi: 0), 0, "Engi Turret off");
        Equal(Get(turret, player, engi: 200), 200, "Engi Turret maximum");
        Equal(Get(turret, drone, engi: 0), 50, "Engi Turret off does not block turret -> drone");
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
        foreach (int value in new[] { 0, 15, 25, 50, 100, 200 }) Equal(valid.Invoke(null, new object[] { value }), true, "Valid percentage");
        foreach (int value in new[] { -5, 1, 201, 205, 65535 }) Equal(valid.Invoke(null, new object[] { value }), false, "Invalid percentage");
        Type settings = plugin.GetType("PvPHelper.DamageSettings")!;
        Type setting = plugin.GetType("PvPHelper.DamageSetting")!;
        MethodInfo getSetting = settings.GetMethod("Get", flags)!;
        Equal(getSetting.Invoke(null, new[] { Enum.Parse(setting, "Pvp") }), (ushort)50, "PVP setting default");
        Equal(getSetting.Invoke(null, new[] { Enum.Parse(setting, "Dvp") }), (ushort)15, "DVP setting default");
        Equal(getSetting.Invoke(null, new[] { Enum.Parse(setting, "EngiTurret") }), (ushort)25, "Engi Turret setting default");
        foreach (string property in new[] { "PvpPercent", "DvpPercent", "EngiTurretPercent" })
            settings.GetProperty(property, flags)!.GetSetMethod(true)!.Invoke(null, new object[] { (ushort)200 });
        settings.GetMethod("Reset", flags)!.Invoke(null, null);
        Equal(getSetting.Invoke(null, new[] { Enum.Parse(setting, "Pvp") }), (ushort)50, "Fresh-session PVP default");
        Equal(getSetting.Invoke(null, new[] { Enum.Parse(setting, "Dvp") }), (ushort)15, "Fresh-session DVP default");
        Equal(getSetting.Invoke(null, new[] { Enum.Parse(setting, "EngiTurret") }), (ushort)25, "Fresh-session Engi Turret default");
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
