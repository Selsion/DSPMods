using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace DSPOptimizations
{
    [RunPatches(typeof(Patch))]
    [Optimization("MonitorOpt", "Optimizes the game logic for traffic monitors", false, new Type[] { })]
    class MonitorOpt : OptimizationSet
    {
        private const int MAX_TICK_COUNT = 3600;
        private static int[] COPY_BUFFER = new int[MAX_TICK_COUNT];
        private static sbyte[] COPY_BUFFER_2 = new sbyte[MAX_TICK_COUNT];

        private static void ResetArrays(ref MonitorComponent monitor)
        {
            // already in vanilla state in this case
            if (monitor.startIdx == 0)
                return;

            // note: startIdx < periodTickCount, so size1 > 0
            int size1 = monitor.periodTickCount - monitor.startIdx;

            Array.Copy(monitor.periodCargoBytesArray, monitor.startIdx, COPY_BUFFER, 0, size1);
            Array.Copy(monitor.periodCargoBytesArray, 0, COPY_BUFFER, size1, monitor.startIdx);
            Array.Copy(COPY_BUFFER, monitor.periodCargoBytesArray, monitor.periodTickCount);

            Array.Copy(monitor.cargoBytesArray, monitor.startIdx, COPY_BUFFER_2, 0, size1);
            Array.Copy(monitor.cargoBytesArray, 0, COPY_BUFFER_2, size1, monitor.startIdx);
            Array.Copy(COPY_BUFFER_2, monitor.cargoBytesArray, monitor.periodTickCount);

            monitor.startIdx = 0;
        }

        class Patch
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(MonitorComponent), "SetEmpty")]
            [HarmonyPatch(typeof(MonitorComponent), "Import")]
            public static void InitIdxPatch(ref MonitorComponent __instance)
            {
                __instance.startIdx = 0;
            }

            /*[HarmonyTranspiler, HarmonyPatch(typeof(MonitorComponent), "Export")]
            static IEnumerable<CodeInstruction> ExportPatch(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher matcher = new CodeMatcher(instructions, generator);

                string[] fieldNames = { "cargoBytesArray", "periodCargoBytesArray" };
                foreach (string fieldName in fieldNames) 
                {
                    // fixes indexing so that the vanilla ordering is outputted, without neeed
                    matcher.MatchForward(false,
                        new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(MonitorComponent), fieldName))
                    ).Advance(2).InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(MonitorComponent), nameof(MonitorComponent.startIdx))),
                        new CodeInstruction(OpCodes.Sub),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(MonitorComponent), nameof(MonitorComponent.periodTickCount))),
                        new CodeInstruction(OpCodes.Add),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(MonitorComponent), nameof(MonitorComponent.periodTickCount))),
                        new CodeInstruction(OpCodes.Rem)
                    );
                }

                return matcher.InstructionEnumeration();
            }*/

            [HarmonyPrefix]
            [HarmonyPatch(typeof(MonitorComponent), "Export")] // can optionally use the transpiler above for export, but this is simpler
            [HarmonyPatch(typeof(MonitorComponent), "SetPeriodTickCount")]
            public static void ResizeArraysPatch(ref MonitorComponent __instance)
            {
                // this patch will be called very rarely, so resetting the arrays is fine
                ResetArrays(ref __instance);
            }

            [HarmonyPrefix, HarmonyPatch(typeof(UIMonitorWindow), "_OnUpdate")]
            public static void WindowPatch(UIMonitorWindow __instance)
            {
                // resetting the arrays on each tick would normally be expensive,
                // but this is only for one monitor and only when its window is open
                if (__instance.monitorAvailable && __instance.monitorId != 0 && __instance.factory != null)
                    ResetArrays(ref __instance.cargoTraffic.monitorPool[__instance.monitorId]);
            }

            [HarmonyTranspiler, HarmonyPatch(typeof(MonitorComponent), "InternalUpdate")]
            static IEnumerable<CodeInstruction> UpdatePatch(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher matcher = new CodeMatcher(instructions, generator);

                LocalBuilder oldStartIdx = generator.DeclareLocal(typeof(int));

                // copy current value of startIdx into oldStartIdx
                matcher.MatchForward(false,
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(MonitorComponent), nameof(MonitorComponent.cargoFlow)))
                ).Advance(-2).InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(MonitorComponent), nameof(MonitorComponent.startIdx))),
                    new CodeInstruction(OpCodes.Stloc_S, oldStartIdx)
                );

                // replace index of 0 with the start index
                matcher.Advance(5).SetInstruction(
                    new CodeInstruction(OpCodes.Ldloc_S, oldStartIdx)
                );

                // remove array copy calls
                for (int i = 0; i < 2; i++)
                {
                    matcher.MatchForward(false,
                        new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Array), nameof(Array.Copy), new[] { typeof(Array), typeof(int), typeof(Array), typeof(int), typeof(int) }))
                    );
                    matcher.Advance(-10).SetOpcodeAndAdvance(OpCodes.Nop).RemoveInstructions(10);
                }

                // startIdx = (startIdx + 1) % length
                matcher.InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(MonitorComponent), nameof(MonitorComponent.startIdx))),
                    new CodeInstruction(OpCodes.Ldc_I4_1),
                    new CodeInstruction(OpCodes.Add),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(MonitorComponent), nameof(MonitorComponent.periodTickCount))),
                    new CodeInstruction(OpCodes.Rem),
                    new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(MonitorComponent), nameof(MonitorComponent.startIdx)))
                );

                // change index from end of array to oldStartIdx
                for (int i = 0; i < 2; i++)
                {
                    matcher.Advance(2).RemoveInstructions(4).InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldloc_S, oldStartIdx)
                    ).Advance(2);
                }

                matcher.MatchForward(false,
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(MonitorComponent), nameof(MonitorComponent.cargoBytesArray)))
                );
                for (int i = 0; i < 2; i++)
                {
                    matcher.Advance(2).RemoveInstructions(4).InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldloc_S, oldStartIdx)
                    ).Advance(3);
                }

                return matcher.InstructionEnumeration();
            }
        }
    }
}
