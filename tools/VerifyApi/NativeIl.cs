using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using Mono.Cecil;

internal sealed class NativeIl
{
    private readonly Type instructionType = Assembly.Load("0Harmony").GetType("HarmonyLib.CodeInstruction")!;
    private readonly FieldInfo operand;
    private readonly FieldInfo opcode;
    private readonly FieldInfo labels;
    internal IList Input { get; }
    private readonly MethodInfo target;

    internal NativeIl(ModuleDefinition game, string typeName, string methodName)
    {
        operand = instructionType.GetField("operand")!;
        opcode = instructionType.GetField("opcode")!;
        labels = instructionType.GetField("labels")!;
        Input = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(instructionType))!;
        var native = game.Types.Single(t => t.FullName == typeName).Methods.Single(m => m.Name == methodName);
        var runtime = Assembly.Load("RoR2").GetType(typeName)!.GetMethod(methodName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)!;
        target = runtime;
        var module = runtime.Module;
        var generator = new DynamicMethod("NativeHookCheck", typeof(void), Type.EmptyTypes).GetILGenerator();
        var locals = runtime.GetMethodBody()!.LocalVariables.Select(l => generator.DeclareLocal(l.LocalType, l.IsPinned)).ToArray();
        var targets = native.Body.Instructions.ToDictionary(i => i, _ => generator.DefineLabel());
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => o.Name!);
        foreach (var instruction in native.Body.Instructions)
        {
            object? value = instruction.Operand switch
            {
                FieldReference field => module.ResolveField(field.MetadataToken.ToInt32()),
                MethodReference call => module.ResolveMethod(call.MetadataToken.ToInt32()),
                TypeReference type => module.ResolveType(type.MetadataToken.ToInt32()),
                Mono.Cecil.Cil.VariableDefinition local => locals[local.Index],
                ParameterDefinition parameter => (short)(parameter.Index + (runtime.IsStatic ? 0 : 1)),
                Mono.Cecil.Cil.Instruction target => targets[target],
                Mono.Cecil.Cil.Instruction[] branches => branches.Select(t => targets[t]).ToArray(),
                _ => instruction.Operand
            };
            var code = Activator.CreateInstance(instructionType, opcodes[instruction.OpCode.Name], value)!;
            ((IList)labels.GetValue(code)!).Add(targets[instruction]);
            Input.Add(code);
        }
    }

    internal object? Operand(object instruction) => operand.GetValue(instruction);
    internal OpCode Opcode(object instruction) => (OpCode)opcode.GetValue(instruction)!;

    internal object[] Apply(Assembly plugin, string patchName)
    {
        var transpiler = plugin.GetType("PvPHelper." + patchName)!.GetMethod("Transpiler", BindingFlags.NonPublic | BindingFlags.Static)!;
        var originalLabels = Input.Cast<object>().SelectMany(i => ((IList)labels.GetValue(i)!).Cast<Label>()).ToArray();
        object[] args = transpiler.GetParameters().Length == 1 ? new object[] { Input } : new object[] { Input, target };
        var output = ((IEnumerable)transpiler.Invoke(null, args)!).Cast<object>().ToArray();
        var patchedLabels = output.SelectMany(i => ((IList)labels.GetValue(i)!).Cast<Label>()).ToArray();
        if (patchedLabels.Length != originalLabels.Length || originalLabels.Any(l => patchedLabels.Count(p => p.Equals(l)) != 1))
            throw new Exception("Native hook lost branch labels: " + patchName);
        try
        {
            var empty = Activator.CreateInstance(typeof(List<>).MakeGenericType(instructionType))!;
            args[0] = empty;
            ((IEnumerable)transpiler.Invoke(null, args)!).Cast<object>().ToArray();
            throw new Exception("Native hook accepted an unexpected shape: " + patchName);
        }
        catch (InvalidOperationException) { }
        return output;
    }
}
