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

        private static bool IsCappedPvp(DamageInfo info, HealthComponent victim)
        {
            if (!NetworkServer.active || !info.attacker || !victim.body || !victim.body.teamComponent) return false;
            CharacterBody attacker = info.attacker.GetComponent<CharacterBody>();
            return attacker && attacker.teamComponent && DamagePolicy.ShouldCap(Classify(attacker), Classify(victim.body),
                PlayerTeams.IsPlayerTeam(attacker.teamComponent.teamIndex) && PlayerTeams.IsPlayerTeam(victim.body.teamComponent.teamIndex),
                attacker == victim.body);
        }

        private static float CapDamage(float damage, DamageInfo info, HealthComponent victim) =>
            IsCappedPvp(info, victim) ? DamagePolicy.CapHit(damage, victim.fullCombinedHealth) : damage;

        // Executions can otherwise remove all remaining health after numeric damage is capped.
        private static bool AllowExecution(bool native, DamageInfo info, HealthComponent victim) =>
            native && !IsCappedPvp(info, victim);

        private static float LimitExecutionThreshold(float native, DamageInfo info, HealthComponent victim) =>
            IsCappedPvp(info, victim) ? float.NegativeInfinity : native;

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

            private static CodeInstruction StoreLoadedLocal(CodeInstruction load)
            {
                if (load.opcode == OpCodes.Ldloc_0) return new CodeInstruction(OpCodes.Stloc_0);
                if (load.opcode == OpCodes.Ldloc_1) return new CodeInstruction(OpCodes.Stloc_1);
                if (load.opcode == OpCodes.Ldloc_2) return new CodeInstruction(OpCodes.Stloc_2);
                if (load.opcode == OpCodes.Ldloc_3) return new CodeInstruction(OpCodes.Stloc_3);
                return new CodeInstruction(load.opcode == OpCodes.Ldloc_S ? OpCodes.Stloc_S : OpCodes.Stloc, load.operand);
            }

            private static bool SameLocal(CodeInstruction left, CodeInstruction right) =>
                left.opcode == right.opcode && Equals(left.operand, right.operand);

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var code = new List<CodeInstruction>(instructions);
                FieldInfo damageField = typeof(DamageInfo).GetField(nameof(DamageInfo.damage));
                FieldInfo bodyField = typeof(HealthComponent).GetField(nameof(HealthComponent.body));
                MethodInfo armorGetter = typeof(CharacterBody).GetProperty(nameof(CharacterBody.armor)).GetGetMethod();
                FieldInfo shieldRegen = typeof(HealthComponent).GetField("isShieldRegenForced", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo fractionGetter = typeof(HealthComponent).GetProperty(nameof(HealthComponent.combinedHealthFraction)).GetGetMethod();
                int copy = -1, armor = -1, copies = 0, armorReads = 0, cap = -1, capMatches = 0, execution = -1, executionMatches = 0;
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
                    if (i >= 7 && code[i].opcode == OpCodes.Stfld && Equals(code[i].operand, shieldRegen) &&
                        IsStore(code[i - 6]) && code[i - 7].opcode.Name.StartsWith("ldloc", StringComparison.Ordinal))
                    {
                        cap = i - 7; capMatches++;
                    }
                    if (i >= 6 && i + 4 < code.Count && code[i].Calls(fractionGetter) &&
                        (code[i + 2].opcode == OpCodes.Bgt_Un || code[i + 2].opcode == OpCodes.Bgt_Un_S) &&
                        code[i + 3].opcode == OpCodes.Ldc_I4_1 && IsStore(code[i + 4]) &&
                        (code[i - 5].opcode == OpCodes.Brtrue || code[i - 5].opcode == OpCodes.Brtrue_S))
                    {
                        execution = i - 6; executionMatches++;
                    }
                }
                // The native build captures DamageInfo for local functions. Load that current
                // reference rather than the original argument, which another hook may replace.
                if (copies != 1 || armorReads != 1 || armor <= copy || capMatches != 1 || executionMatches != 1 ||
                    cap <= armor || execution <= cap ||
                    !SameLocal(code[cap], LoadStoredLocal(code[copy + 1])) ||
                    !SameLocal(code[execution], LoadStoredLocal(code[execution + 10])) ||
                    !SameLocal(code[execution + 2], code[execution + 7]) ||
                    !code[copy - 2].opcode.Name.StartsWith("ldloc", StringComparison.Ordinal) ||
                    code[copy - 1].opcode != OpCodes.Ldfld || !(code[copy - 1].operand is FieldInfo captured) || captured.FieldType != typeof(DamageInfo) ||
                    !code[armor - 4].opcode.Name.StartsWith("ldloc", StringComparison.Ordinal) ||
                    (code[armor - 3].opcode != OpCodes.Brtrue && code[armor - 3].opcode != OpCodes.Brtrue_S) ||
                    code[armor - 2].opcode != OpCodes.Ldarg_0 || !Equals(code[armor - 1].operand, bodyField))
                    throw new InvalidOperationException("PvP damage hook no longer matches the game API.");

                CodeInstruction store = code[copy + 1];
                MethodInfo normal = typeof(DamageScaling).GetMethod(nameof(ScaleDamage), BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo bypass = typeof(DamageScaling).GetMethod(nameof(ScaleBypassDamage), BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo capDamage = typeof(DamageScaling).GetMethod(nameof(CapDamage), BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo allowExecution = typeof(DamageScaling).GetMethod(nameof(AllowExecution), BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo limitThreshold = typeof(DamageScaling).GetMethod(nameof(LimitExecutionThreshold), BindingFlags.Static | BindingFlags.NonPublic);
                for (int i = 0; i < code.Count; i++)
                {
                    if (i == cap || i == execution)
                    {
                        // Final damage before health/shield/barrier consumption and delayed
                        // damage splitting; includes both normal and calculation-bypass hits.
                        var load = i == cap ? LoadStoredLocal(store) : LoadStoredLocal(code[execution + 10]);
                        code[i].MoveLabelsTo(load);
                        code[i].MoveBlocksTo(load);
                        yield return load;
                        yield return new CodeInstruction(code[copy - 2].opcode, code[copy - 2].operand);
                        yield return new CodeInstruction(code[copy - 1].opcode, code[copy - 1].operand);
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Call, i == cap ? capDamage : allowExecution);
                        yield return i == cap ? new CodeInstruction(store.opcode, store.operand) :
                            new CodeInstruction(code[execution + 10].opcode, code[execution + 10].operand);
                        if (i == execution)
                        {
                            yield return new CodeInstruction(code[execution + 2].opcode, code[execution + 2].operand);
                            yield return new CodeInstruction(code[copy - 2].opcode, code[copy - 2].operand);
                            yield return new CodeInstruction(code[copy - 1].opcode, code[copy - 1].operand);
                            yield return new CodeInstruction(OpCodes.Ldarg_0);
                            yield return new CodeInstruction(OpCodes.Call, limitThreshold);
                            yield return StoreLoadedLocal(code[execution + 2]);
                        }
                    }
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
