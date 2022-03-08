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
    [Optimization("SphereBufferOpt", "Reduces lag for hidden layers under construction", false, new Type[] { })]
    class SphereBufferOpt : OptimizationSet
    {

        class Patch
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(DysonSphereSegmentRenderer), nameof(DysonSphereSegmentRenderer.Init))]
            public static void InitBatchMasks_Postfix(DysonSphereSegmentRenderer __instance)
            {
                __instance.layersDirtyMask = new int[DysonSphereSegmentRenderer.protoMeshes.Length];
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(DysonSphere), "UpdateProgress", new[] { typeof(DysonNode) })]
            public static void BatchMask_UpdateProgress_Postfix1(DysonSphere __instance, DysonNode node)
            {
                __instance.modelRenderer.layersDirtyMask[node.protoId] |= 1 << node.layerId;
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(DysonSphere), "UpdateProgress", new[] { typeof(DysonFrame) })]
            public static void BatchMask_UpdateProgress_Postfix2(DysonSphere __instance, DysonFrame frame)
            {
                __instance.modelRenderer.layersDirtyMask[frame.protoId] |= 1 << frame.layerId;
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(DysonSphereSegmentRenderer), nameof(DysonSphereSegmentRenderer.RebuildModels))]
            public static void DysonSphereSegmentRenderer_RebuildModels_Postfix(DysonSphereSegmentRenderer __instance)
            {
                for (int i = 0; i < DysonSphereSegmentRenderer.totalProtoCount; i++)
                    __instance.layersDirtyMask[i] = ~0;
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.UpdateStates), new[] { typeof(DysonNode), typeof(uint), typeof(bool), typeof(bool) })]
            public static void DysonSphere_UpdateStates_Postfix1(DysonSphere __instance, DysonNode node)
            {
                __instance.modelRenderer.layersDirtyMask[node.protoId] |= 1 << node.layerId;
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.UpdateStates), new[] { typeof(DysonFrame), typeof(uint), typeof(bool), typeof(bool) })]
            public static void DysonSphere_UpdateStates_Postfix2(DysonSphere __instance, DysonFrame frame)
            {
                __instance.modelRenderer.layersDirtyMask[frame.protoId] |= 1 << frame.layerId;
            }

            [HarmonyTranspiler, HarmonyPatch(typeof(DysonSphereSegmentRenderer), "DrawModels")]
            static IEnumerable<CodeInstruction> DrawModels_SyncBuffers_Patch(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher matcher = new CodeMatcher(instructions, generator);

                matcher.MatchForward(false,
                    new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(DysonSphereSegmentRenderer.Batch), nameof(DysonSphereSegmentRenderer.Batch.SyncBufferData)))
                ).Advance(-3).SetOpcodeAndAdvance(OpCodes.Nop)
                .Advance(1).RemoveInstructions(2)
                .InsertAndAdvance(
                    // already have arg0, the idx
                    new CodeInstruction(OpCodes.Ldarg_2), // editorMask
                    new CodeInstruction(OpCodes.Ldarg_3), // gameMask
                    Transpilers.EmitDelegate<Action<DysonSphereSegmentRenderer, int, int, int>>((DysonSphereSegmentRenderer renderer, int batchIdx, int editorMask, int gameMask) =>
                    {
                        int visibleMask = DysonSphere.renderPlace == ERenderPlace.Dysonmap ? editorMask : gameMask;
                        if((visibleMask & renderer.layersDirtyMask[batchIdx]) != 0)
                        {
                            renderer.batches[batchIdx].SyncBufferData();
                            renderer.layersDirtyMask[batchIdx] = 0;
                        }
                    })
                );

                return matcher.InstructionEnumeration();
            }
        }
    }
}
