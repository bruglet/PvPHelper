using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RoR2;
using UnityEngine.Networking;

namespace PvPHelper
{
    internal static class DamageScaling
    {
        private static DamageActor Classify(CharacterBody body) => DamagePolicy.Classify(body.isPlayerControlled,
            !body.isPlayerControlled && DroneCatalog.GetDroneIndexFromBodyIndex(body.bodyIndex) != DroneIndex.None,
            !body.isPlayerControlled && IsEngineerTurret(body.bodyIndex));

        private static bool IsEngineerTurret(BodyIndex index) => index != BodyIndex.None &&
            (index == BodyCatalog.FindBodyIndex("EngiTurretBody") || index == BodyCatalog.FindBodyIndex("EngiWalkerTurretBody"));

        private static int PercentFor(DamageInfo info, HealthComponent victim)
        {
            if (!info.attacker || !victim.body || !victim.body.teamComponent) return 100;
            CharacterBody attacker = info.attacker.GetComponent<CharacterBody>();
            if (!attacker || !attacker.teamComponent) return 100;
            return DamagePolicy.GetPercent(Classify(attacker), Classify(victim.body),
                PlayerTeams.IsPlayerTeam(attacker.teamComponent.teamIndex) && PlayerTeams.IsPlayerTeam(victim.body.teamComponent.teamIndex),
                attacker == victim.body, info.delayedDamageSecondHalf, DamageSettings.PvpPercent, DamageSettings.DvpPercent,
                DamageSettings.EngiTurretPercent);
        }

        private static float ScaleDamage(float damage, DamageInfo info, HealthComponent victim) =>
            NetworkServer.active ? damage * (PercentFor(info, victim) / 100f) : damage;

        private static float ScaleBypassDamage(float damage, DamageInfo info, HealthComponent victim) =>
            (info.damageType.damageTypeExtended & DamageTypeExtended.BypassDamageCalculations) != DamageTypeExtended.Generic
                ? ScaleDamage(damage, info, victim) : damage;

        [HarmonyPatch(typeof(HealthComponent), nameof(HealthComponent.TakeDamage))]
        private static class RejectZeroDamage
        {
            private static bool Prefix(HealthComponent __instance, DamageInfo __0)
            {
                if (!NetworkServer.active || PercentFor(__0, __instance) != 0) return true;
                // Callers use rejection to suppress on-hit effects as well as numeric damage.
                __0.rejected = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(HealthComponent), "TakeDamageProcess")]
        private static class ScaleCalculatedDamage
        {
            private static bool IsStore(CodeInstruction instruction) => instruction.opcode.Name.StartsWith("stloc", StringComparison.Ordinal);

            private static CodeInstruction LoadStoredLocal(CodeInstruction store)
            {
                if (store.opcode == OpCodes.Stloc_0) return new CodeInstruction(OpCodes.Ldloc_0);
                if (store.opcode == OpCodes.Stloc_1) return new CodeInstruction(OpCodes.Ldloc_1);
                if (store.opcode == OpCodes.Stloc_2) return new CodeInstruction(OpCodes.Ldloc_2);
                if (store.opcode == OpCodes.Stloc_3) return new CodeInstruction(OpCodes.Ldloc_3);
                return new CodeInstruction(store.opcode == OpCodes.Stloc_S ? OpCodes.Ldloc_S : OpCodes.Ldloc, store.operand);
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var code = new List<CodeInstruction>(instructions);
                FieldInfo damageField = typeof(DamageInfo).GetField(nameof(DamageInfo.damage));
                FieldInfo bodyField = typeof(HealthComponent).GetField(nameof(HealthComponent.body));
                MethodInfo armorGetter = typeof(CharacterBody).GetProperty(nameof(CharacterBody.armor)).GetGetMethod();
                int copy = -1, armor = -1, copies = 0, armorReads = 0;
                for (int i = 2; i < code.Count - 1; i++)
                {
                    if (code[i].opcode == OpCodes.Ldfld && Equals(code[i].operand, damageField) && IsStore(code[i + 1]))
                    {
                        copy = i; copies++;
                    }
                    if (code[i].opcode == OpCodes.Callvirt && Equals(code[i].operand, armorGetter))
                    {
                        armor = i; armorReads++;
                    }
                }
                // The native build captures DamageInfo for local functions. Load that current
                // reference rather than the original argument, which another hook may replace.
                if (copies != 1 || armorReads != 1 || armor <= copy ||
                    !code[copy - 2].opcode.Name.StartsWith("ldloc", StringComparison.Ordinal) ||
                    code[copy - 1].opcode != OpCodes.Ldfld || !(code[copy - 1].operand is FieldInfo captured) || captured.FieldType != typeof(DamageInfo) ||
                    !code[armor - 4].opcode.Name.StartsWith("ldloc", StringComparison.Ordinal) ||
                    (code[armor - 3].opcode != OpCodes.Brtrue && code[armor - 3].opcode != OpCodes.Brtrue_S) ||
                    code[armor - 2].opcode != OpCodes.Ldarg_0 || !Equals(code[armor - 1].operand, bodyField))
                    throw new InvalidOperationException("PvP damage hook no longer matches the game API.");

                CodeInstruction store = code[copy + 1];
                MethodInfo normal = typeof(DamageScaling).GetMethod(nameof(ScaleDamage), BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo bypass = typeof(DamageScaling).GetMethod(nameof(ScaleBypassDamage), BindingFlags.Static | BindingFlags.NonPublic);
                for (int i = 0; i < code.Count; i++)
                {
                    // Normal hits: after offensive bonuses (including Expose), before armor,
                    // flat damage reduction and protection caps. Do not mutate DamageInfo.damage.
                    if (i == armor - 4)
                    {
                        var load = LoadStoredLocal(store);
                        code[i].MoveLabelsTo(load);
                        code[i].MoveBlocksTo(load);
                        yield return load;
                        yield return new CodeInstruction(code[copy - 2].opcode, code[copy - 2].operand);
                        yield return new CodeInstruction(code[copy - 1].opcode, code[copy - 1].operand);
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Call, normal);
                        yield return new CodeInstruction(store.opcode, store.operand);
                    }
                    // Calculation-bypassing hits skip the normal block; scale only those here.
                    if (i == copy + 1)
                    {
                        var load = new CodeInstruction(code[copy - 2].opcode, code[copy - 2].operand);
                        code[i].MoveLabelsTo(load);
                        code[i].MoveBlocksTo(load);
                        yield return load;
                        yield return new CodeInstruction(code[copy - 1].opcode, code[copy - 1].operand);
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Call, bypass);
                    }
                    yield return code[i];
                }
            }
        }
    }
}
