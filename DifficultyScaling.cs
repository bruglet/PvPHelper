using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RoR2;

namespace PvPHelper
{
    internal static class DifficultyScaling
    {
        internal const ushort DefaultPercent = 75;

        internal static float ScaleProgress(float progress, int percent) => progress * (percent / 100f);

        private static float ScaleProgress(float progress) => ScaleProgress(progress, DamageSettings.MonsterScalingPercent);

        [HarmonyPatch(typeof(Run), "RecalculateDifficultyCoefficentInternal")]
        private static class SlowGrowth
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var code = new List<CodeInstruction>(instructions);
                MethodInfo stopwatch = typeof(Run).GetMethod(nameof(Run.GetRunStopwatch));
                FieldInfo stages = typeof(Run).GetField(nameof(Run.stageClearCount));
                MethodInfo scale = typeof(DifficultyScaling).GetMethod(nameof(ScaleProgress),
                    BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(float) }, null);
                int clocks = 0, stageReads = 0;
                for (int i = 0; i < code.Count; i++)
                {
                    if (code[i].Calls(stopwatch)) clocks++;
                    if (code[i].opcode == OpCodes.Ldfld && Equals(code[i].operand, stages))
                    {
                        if (i + 1 >= code.Count || code[i + 1].opcode != OpCodes.Conv_R4)
                            throw new InvalidOperationException("Monster scaling stage hook no longer matches the game API.");
                        stageReads++;
                    }
                }
                if (clocks != 1 || stageReads != 2)
                    throw new InvalidOperationException("Monster scaling hook no longer matches the game API.");

                // Only the difficulty formula sees scaled progress. Keep the real stopwatch,
                // stage count, player factor and difficulty mode untouched. Both coefficient
                // and ambient monster level calculations consume these scaled inputs.
                for (int i = 0; i < code.Count; i++)
                {
                    yield return code[i];
                    if (code[i].Calls(stopwatch) || (i > 0 && code[i].opcode == OpCodes.Conv_R4 &&
                        code[i - 1].opcode == OpCodes.Ldfld && Equals(code[i - 1].operand, stages)))
                        yield return new CodeInstruction(OpCodes.Call, scale);
                }
            }
        }
    }
}
