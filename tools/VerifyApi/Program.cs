using System.Reflection;
using System.Runtime.Loader;
if (args.Length != 4) throw new ArgumentException("Usage: VerifyApi <plugin.dll> <game Managed dir> <BepInEx core dir> <R2API Teams DLL dir>");
var dirs = args.Skip(1).Select(Path.GetFullPath).ToArray();
AssemblyLoadContext.Default.Resolving += (context, name) => {
    foreach (var dir in dirs) { var path = Path.Combine(dir, name.Name + ".dll"); if (File.Exists(path)) return context.LoadFromAssemblyPath(path); }
    return null;
};
var plugin = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[0]));
using var pluginModule = Mono.Cecil.ModuleDefinition.ReadModule(Path.GetFullPath(args[0]));
using var gameModule = Mono.Cecil.ModuleDefinition.ReadModule(Path.Combine(dirs[0], "RoR2.dll"));
var patches = 0;
foreach (var typeName in new[] { "PlayerTeams+AssignBeforeBodySpawn", "TeamSelector+AddSelector", "SharedRewards+ShareMoney", "SharedRewards+ShareExperience", "SharedHoldouts+CountAllPlayers", "SharedHoldouts+CountAllPlayersInRadius", "SharedHoldouts+ShowChargeObjective", "SharedHoldouts+ShareFocusedConvergence", "DamageScaling+RejectZeroDamage", "DamageScaling+ScaleCalculatedDamage", "DifficultyScaling+SlowGrowth", "PlayerPickups+AllowPlayerFactions", "HalcyonPlayers+FindAllPlayerFactions", "PlayerItemRules+RecognizePlayerFactions" }) {
    var type = plugin.GetType("PvPHelper." + typeName)!;
    var attrs = type.GetCustomAttributes().Where(a => a.GetType().Name == "HarmonyPatch").ToArray();
    if (attrs.Length == 0) continue;
    var multiple = type.GetMethod("TargetMethods", BindingFlags.Static | BindingFlags.NonPublic);
    if (multiple != null) {
        foreach (MethodBase method in (IEnumerable<MethodBase>)multiple.Invoke(null, null)!) {
            if (method == null) throw new Exception("Missing target: " + type.FullName);
            Console.WriteLine("PASS hook: " + method.DeclaringType!.FullName + "." + method.Name);
            patches++;
        }
        continue;
    }
    MethodBase? target = null;
    var resolver = type.GetMethod("TargetMethod", BindingFlags.Static | BindingFlags.NonPublic);
    if (resolver != null) {
        // Read the actual target resolver's class/method strings without executing Harmony.
        // BepInEx's bundled Harmony targets Unity's Mono runtime, not .NET 8.
        var container = pluginModule.Types.Single(t => t.FullName == type.DeclaringType!.FullName);
        var patch = container.NestedTypes.Single(t => t.Name == type.Name);
        var strings = patch.Methods.Single(m => m.Name == "TargetMethod").Body.Instructions
            .Where(i => i.OpCode == Mono.Cecil.Cil.OpCodes.Ldstr).Select(i => (string)i.Operand).ToArray();
        var declaring = Assembly.Load("RoR2").GetType("RoR2.HoldoutZoneController")!
            .GetNestedType(strings[0], BindingFlags.NonPublic)!;
        target = declaring.GetMethod(strings[1], BindingFlags.NonPublic | BindingFlags.Instance);
        var nativeTarget = gameModule.Types.Single(t => t.FullName == "RoR2.HoldoutZoneController")
            .NestedTypes.Single(t => t.Name == strings[0]).Methods.Single(m => m.Name == strings[1]);
        var itemCalls = nativeTarget.Body.Instructions.Where(i => i.Operand is Mono.Cecil.MethodReference method &&
            method.DeclaringType.FullName == "RoR2.Util" && method.Name == "GetItemCountForTeam").ToArray();
        if (itemCalls.Length != 1 || ((Mono.Cecil.MethodReference)itemCalls[0].Operand).Parameters.Count != 4)
            throw new Exception("Focused Convergence IL no longer matches the transpiler");
    }
    else {
        var info = attrs[0].GetType().GetField("info")!.GetValue(attrs[0])!;
        var infoType = info.GetType();
        var declaringType = (Type)infoType.GetField("declaringType")!.GetValue(info)!;
        var name = (string)infoType.GetField("methodName")!.GetValue(info)!;
        var parameterTypes = (Type[]?)infoType.GetField("argumentTypes")!.GetValue(info);
        target = parameterTypes == null ? declaringType.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) : declaringType.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, parameterTypes, null);
    }
    if (target == null) throw new Exception("Missing target: " + type.FullName);
    Console.WriteLine("PASS hook: " + target.DeclaringType!.FullName + "." + target.Name);
    patches++;
}
var net = Assembly.Load("com.unity.multiplayer-hlapi.Runtime");
var writerType = net.GetType("UnityEngine.Networking.NetworkWriter")!;
var readerType = net.GetType("UnityEngine.Networking.NetworkReader")!;
var idType = net.GetType("UnityEngine.Networking.NetworkInstanceId")!;
var choiceType = plugin.GetType("PvPHelper.PlayerTeams+ChoiceMessage")!;
var stateType = plugin.GetType("PvPHelper.PlayerTeams+StateMessage")!;
object MakeChoice(uint id, byte choice) {
    var value = Activator.CreateInstance(choiceType)!;
    choiceType.GetField("User")!.SetValue(value, Activator.CreateInstance(idType, id));
    choiceType.GetField("Choice")!.SetValue(value, choice);
    return value;
}
object RoundTrip(object original) {
    var writer = Activator.CreateInstance(writerType)!;
    original.GetType().GetMethod("Serialize")!.Invoke(original, new[] { writer });
    var bytes = (byte[])writerType.GetMethod("ToArray")!.Invoke(writer, null)!;
    var reader = Activator.CreateInstance(readerType, new object[] { bytes })!;
    var copy = Activator.CreateInstance(original.GetType())!;
    original.GetType().GetMethod("Deserialize")!.Invoke(copy, new[] { reader });
    return copy;
}
void AssertChoice(object value, uint id, byte choice) {
    var networkId = choiceType.GetField("User")!.GetValue(value)!;
    if ((uint)idType.GetProperty("Value")!.GetValue(networkId)! != id || (byte)choiceType.GetField("Choice")!.GetValue(value)! != choice)
        throw new Exception("Wire round trip mismatch");
}
foreach (byte choice in new byte[] { 0, 1, 2, 3, 255 }) AssertChoice(RoundTrip(MakeChoice(4294967294, choice)), 4294967294, choice);
var snapshot = Activator.CreateInstance(stateType)!;
var list = (System.Collections.IList)stateType.GetField("Choices")!.GetValue(snapshot)!;
for (byte i = 0; i < 4; i++) list.Add(MakeChoice((uint)(i + 100), i));
var result = RoundTrip(snapshot);
var copyList = (System.Collections.IList)stateType.GetField("Choices")!.GetValue(result)!;
if (copyList.Count != 4) throw new Exception("Snapshot count mismatch");
for (byte i = 0; i < 4; i++) AssertChoice(copyList[i]!, (uint)(i + 100), i);
if ((ushort)stateType.GetField("EngiTurretPercent")!.GetValue(snapshot)! != 25)
    throw new Exception("Engineer turret default mismatch");
if ((ushort)stateType.GetField("MonsterScalingPercent")!.GetValue(snapshot)! != 75)
    throw new Exception("Monster scaling default mismatch");
foreach (var pair in new[] { (50, 15, 25, 75), (0, 200, 0, 0), (200, 0, 200, 100), (85, 35, 40, 50) }) {
    stateType.GetField("PvpPercent")!.SetValue(snapshot, (ushort)pair.Item1);
    stateType.GetField("DvpPercent")!.SetValue(snapshot, (ushort)pair.Item2);
    stateType.GetField("EngiTurretPercent")!.SetValue(snapshot, (ushort)pair.Item3);
    stateType.GetField("MonsterScalingPercent")!.SetValue(snapshot, (ushort)pair.Item4);
    var copy = RoundTrip(snapshot);
    if ((ushort)stateType.GetField("PvpPercent")!.GetValue(copy)! != pair.Item1 ||
        (ushort)stateType.GetField("DvpPercent")!.GetValue(copy)! != pair.Item2 ||
        (ushort)stateType.GetField("EngiTurretPercent")!.GetValue(copy)! != pair.Item3 ||
        (ushort)stateType.GetField("MonsterScalingPercent")!.GetValue(copy)! != pair.Item4)
        throw new Exception("Damage settings wire round trip mismatch");
    var choices = (System.Collections.IList)stateType.GetField("Choices")!.GetValue(copy)!;
    if (choices.Count != 4) throw new Exception("Settings corrupted team snapshot");
    for (byte i = 0; i < 4; i++) AssertChoice(choices[i]!, (uint)(i + 100), i);
}
if (((System.Collections.IList)stateType.GetField("Choices")!.GetValue(RoundTrip(Activator.CreateInstance(stateType)!))!).Count != 0) throw new Exception("Empty snapshot mismatch");
Console.WriteLine($"PASS {patches} Harmony targets and request/snapshot wire round trips");
DamageChecks.Run(plugin, gameModule);
CompatibilityChecks.Run(plugin, gameModule);
