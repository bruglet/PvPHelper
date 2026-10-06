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
foreach (var typeName in new[] { "Plugin+RememberLastPlayerDeath", "Plugin+ContinueAfterPartyWipe", "PlayerTeams+AssignBeforeBodySpawn", "TeamSelector+AddSelector", "SharedRewards+ShareMoney", "SharedRewards+ShareExperience", "SharedHoldouts+CountAllPlayers", "SharedHoldouts+CountAllPlayersInRadius", "SharedHoldouts+ShowChargeObjective", "SharedHoldouts+ShareFocusedConvergence", "DamageScaling+RejectZeroDamage", "DamageScaling+ScaleCalculatedDamage", "DifficultyScaling+SlowGrowth", "PlayerPickups+AllowPlayerFactions", "HalcyonPlayers+FindAllPlayerFactions", "PlayerItemRules+RecognizePlayerFactions", "StageStartingMoney+FundStageEntry" }) {
    if (typeName.StartsWith("Plugin+", StringComparison.Ordinal)) {
        // Resolving Plugin's BaseUnityPlugin base class requires BepInEx's Unity/Mono
        // runtime. Read these patch attributes from IL instead on the .NET 8 verifier.
        var patch = pluginModule.Types.Single(t => t.FullName == "PvPHelper.Plugin").NestedTypes.Single(t => t.Name == typeName.Split('+')[1]);
        var attribute = patch.CustomAttributes.Single(a => a.AttributeType.Name == "HarmonyPatch");
        var declaring = (Mono.Cecil.TypeReference)attribute.ConstructorArguments[0].Value;
        var methodName = (string)attribute.ConstructorArguments[1].Value;
        var targetType = gameModule.Types.Single(t => t.FullName == declaring.FullName);
        if (!targetType.Methods.Any(m => m.Name == methodName)) throw new Exception("Missing wipe target: " + methodName);
        Console.WriteLine("PASS hook: " + declaring.FullName + "." + methodName);
        patches++;
        continue;
    }
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
var defaultTeam = plugin.GetType("PvPHelper.PlayerTeams")!.GetMethod("PickDefaultTeam", BindingFlags.NonPublic | BindingFlags.Static)!;
byte PickTeam(IEnumerable<byte> activeChoices) => (byte)defaultTeam.Invoke(null, new object[] { activeChoices })!;
var arrivals = new List<byte>();
for (int i = 0; i < 12; i++) {
    byte next = PickTeam(arrivals);
    if (next != i % 4) throw new Exception("Fresh lobby defaults must separate the first four players and balance later arrivals");
    arrivals.Add(next);
}
foreach (var example in new[] {
    (new byte[] { 0, 0 }, (byte)1), // Manual same-team choices leave Blue free.
    (new byte[] { 0, 2, 3 }, (byte)1), // A departure frees Blue for a new arrival.
    (new byte[] { 0, 0, 1, 2, 3 }, (byte)1), // Choose the least-populated occupied team.
    (new byte[] { 3, 3 }, (byte)0), // Color order resolves equal populations.
    (new byte[] { 255 }, (byte)0)
}) {
    if (PickTeam(example.Item1) != example.Item2) throw new Exception("Lobby default must respect active occupancy and manual choices");
}
var teamModule = pluginModule.Types.Single(t => t.FullName == "PvPHelper.PlayerTeams");
var joined = teamModule.Methods.Single(m => m.Name == "UserStarted");
var joinedCalls = joined.Body.Instructions.Select(i => i.Operand).OfType<Mono.Cecil.MethodReference>().ToArray();
if (!joinedCalls.Any(m => m.Name == "ContainsKey") || !joinedCalls.Any(m => m.Name == "PickDefaultTeam") ||
    !joined.Body.Instructions.Any(i => i.Operand is Mono.Cecil.FieldReference f && f.DeclaringType.FullName == "RoR2.NetworkUser" && f.Name == "readOnlyInstancesList") ||
    joinedCalls.Any(m => m.Name == "get_Values"))
    throw new Exception("Defaults must preserve stored choices and count only active network users");
Console.WriteLine("PASS lobby defaults: separate first four players, balance extras, honor manual occupancy and exclude departed users");
Console.WriteLine($"PASS {patches} Harmony targets and request/snapshot wire round trips");
DamageChecks.Run(plugin, gameModule);
CompatibilityChecks.Run(plugin, gameModule);
WipeExitChecks.Run(pluginModule, gameModule);
StageMoneyChecks.Run(plugin, pluginModule, gameModule);
