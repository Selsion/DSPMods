using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace DSPOptimizations
{

    /*[RunPatches(typeof(Patch))]
    class PerformanceStatsFix
    {
        class Patch
        {
            [HarmonyTranspiler, HarmonyPatch(typeof(GameData), "GameTick")]
            static IEnumerable<CodeInstruction> StatsFixPatch(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher matcher = new CodeMatcher(instructions, generator);

                var beginMethod = AccessTools.Method(typeof(PerformanceMonitor), nameof(PerformanceMonitor.BeginSample));
                var endMethod = AccessTools.Method(typeof(PerformanceMonitor), nameof(PerformanceMonitor.EndSample));

                matcher.MatchForward(false,
                    new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)ECpuWorkEntry.Splitter),
                    new CodeMatch(OpCodes.Call, beginMethod)
                ).RemoveInstructions(2);

                matcher.MatchForward(false,
                    new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)ECpuWorkEntry.Splitter),
                    new CodeMatch(OpCodes.Call, endMethod)
                ).RemoveInstructions(2);

                matcher.MatchForward(false,
                    new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)ECpuWorkEntry.Belt),
                    new CodeMatch(OpCodes.Call, beginMethod)
                ).RemoveInstructions(2);
                    
                matcher.MatchForward(false,
                    new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)ECpuWorkEntry.Belt),
                    new CodeMatch(OpCodes.Call, endMethod)
                ).RemoveInstructions(2);

                return matcher.InstructionEnumeration();
            }
        }
    }*/
}
