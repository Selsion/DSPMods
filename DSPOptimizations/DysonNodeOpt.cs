﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace DSPOptimizations
{
    [RunPatches(typeof(Patch))]
    [Optimization("DysonNodeOpt", "Optimizes the game logic for dyson nodes", false, new Type[] { })]
    class DysonNodeOpt : OptimizationSet
    {

        //private static int NODE_BUF_LOOP_INC = 20;

        public static void InitSPAndCPCounts()
        {
            foreach (var sphere in GameMain.data.dysonSpheres)
            {
                if (sphere == null)
                    continue;

                for(int i = 1; i < sphere.layersIdBased.Length; i++)
                {
                    var layer = sphere.layersIdBased[i];
                    if (layer == null || layer.id != i)
                        continue;

                    layer.totalNodeSP = 0;
                    layer.totalFrameSP = 0;
                    layer.totalCP = 0;

                    DysonNode[] nodePool = layer.nodePool;
                    for (int j = 1; j < layer.nodeCursor; j++)
                        if (nodePool[j] != null && nodePool[j].id == j)
                            layer.totalNodeSP += nodePool[j].sp;

                    DysonFrame[] framePool = layer.framePool;
                    for (int j = 1; j < layer.frameCursor; j++)
                        if (framePool[j] != null && framePool[j].id == j)
                            layer.totalFrameSP += framePool[j].spA + framePool[j].spB;

                    DysonShell[] shellPool = layer.shellPool;
                    for (int k = 1; k < layer.shellCursor; k++)
                        if (shellPool[k] != null && shellPool[k].id == k)
                            layer.totalCP += shellPool[k].cellPoint;
                }
            }
        }

        class Patch
        {
            [HarmonyTranspiler, HarmonyPatch(typeof(DysonSphereLayer), "GameTick")]
            static IEnumerable<CodeInstruction> NodeIterSkipPatch(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher matcher = new CodeMatcher(instructions, generator);

                // find the local variable storing the time gene
                matcher.MatchForward(true,
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(DysonNode), nameof(DysonNode.id))),
                    new CodeMatch(OpCodes.Ldc_I4_S),
                    new CodeMatch(OpCodes.Rem)
                ).Advance(-1);
                OpCode skipCountOpcode = matcher.Opcode;
                object skipCountOperand = matcher.Operand;
                matcher.Advance(2);
                OpCode timeGeneOpcode = matcher.Opcode;
                object timeGeneOperand = matcher.Operand;

                // set the start of the for loop to the time gene
                matcher.Start().MatchForward(true,
                    new CodeMatch(OpCodes.Ldarg_1),
                    new CodeMatch(OpCodes.Ldc_I4_S),
                    new CodeMatch(OpCodes.Conv_I8),
                    new CodeMatch(OpCodes.Rem),
                    new CodeMatch(OpCodes.Conv_I4)
                ).Advance(2).SetInstructionAndAdvance(
                    new CodeInstruction(timeGeneOpcode, timeGeneOperand)
                );

                // set the for loop increment to skipCount
                matcher.MatchForward(true,
                    new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(DysonNode), nameof(DysonNode.OrderConstructCp)))
                ).Advance(2).SetInstructionAndAdvance(
                    new CodeInstruction(skipCountOpcode, skipCountOperand)
                );

                return matcher.InstructionEnumeration();
            }

            /*[HarmonyPostfix]
            [HarmonyPatch(typeof(DysonSphereLayer), "Import")]
            public static void DysonSphereLayere_Import_Postfix(DysonSphereLayer __instance)*/

            [HarmonyPostfix]
            [HarmonyPatch(typeof(DysonSphere), "UpdateProgress", new[] { typeof(DysonNode) })]
            public static void DysonSphere_UpdateProgress_Postfix1(DysonSphere __instance, DysonNode node)
            {
                lock (__instance)
                {
                    __instance.layersIdBased[node.layerId].totalNodeSP++;
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(DysonSphere), "UpdateProgress", new[] { typeof(DysonFrame) })]
            public static void DysonSphere_UpdateProgress_Postfix1(DysonSphere __instance, DysonFrame frame)
            {
                lock (__instance)
                {
                    __instance.layersIdBased[frame.layerId].totalFrameSP++;
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(DysonShell), "Construct")]
            public static void DysonShell_Construct_Postfix(DysonShell __instance, bool __result)
            {
                if (__result)
                    __instance.parentLayer.totalCP++;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(DysonSphereLayer), "RemoveDysonNode")]
            public static void DysonSphereLayer_RemoveDysonNode_Prefix(ref DysonSphereLayer __instance, int nodeId)
            {
                if (__instance.nodePool[nodeId].id != 0)
                    __instance.totalNodeSP -= __instance.nodePool[nodeId].sp;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(DysonSphereLayer), "RemoveDysonFrame")]
            public static void DysonSphereLayer_RemoveDysonFrame_Prefix(ref DysonSphereLayer __instance, int frameId)
            {
                if (__instance.framePool[frameId].id != 0)
                    __instance.totalFrameSP -= __instance.framePool[frameId].spA + __instance.framePool[frameId].spB;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(DysonSphereLayer), "RemoveDysonShell")]
            public static void DysonSphereLayer_RemoveDysonShell_Prefix(ref DysonSphereLayer __instance, int shellId)
            {
                if (__instance.shellPool[shellId].id != 0)
                    __instance.totalCP -= __instance.shellPool[shellId].cellPoint;
            }

            [HarmonyTranspiler, HarmonyPatch(typeof(DysonSphere), "BeforeGameTick")]
            static IEnumerable<CodeInstruction> SpherePowerPatch(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher matcher = new CodeMatcher(instructions, generator);

                // find the last line that we need to jump to
                matcher.MatchForward(false,
                    new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(DysonSphereLayer), "get_energyGenCurrentTick")),
                    new CodeMatch(OpCodes.Add),
                    new CodeMatch(OpCodes.Stfld, AccessTools.Field(typeof(DysonSphere), nameof(DysonSphere.energyGenCurrentTick)))
                ).Advance(-4);
                matcher.CreateLabel(out Label end);

                // find the first line that we need to skip
                matcher.Start().MatchForward(false,
                    new CodeMatch(OpCodes.Ldc_I4_0),
                    new CodeMatch(OpCodes.Conv_I8),
                    new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(DysonSphereLayer), "set_energyGenCurrentTick"))
                ).Advance(-1);
                // get the layer local variable
                OpCode layerOpcode = matcher.Opcode;
                object layerOperand = matcher.Operand;
                
                // insert the code to compute the power and skip to the end
                matcher.Insert(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(layerOpcode, layerOperand),
                    Transpilers.EmitDelegate<Action<DysonSphere, DysonSphereLayer>>((DysonSphere sphere, DysonSphereLayer layer) =>
                    {
                        layer.energyGenCurrentTick = layer.totalNodeSP * sphere.energyGenPerNode + layer.totalFrameSP * sphere.energyGenPerFrame + layer.totalCP * sphere.energyGenPerShell;
                    }),
                    new CodeInstruction(OpCodes.Br, end)
                ).CreateLabel(out Label start);

                // fix the jump to go to the start of our code rather than right after it
                matcher.Start().MatchForward(true,
                    new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(DysonSphereLayer), "get_grossRadius")),
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(DysonSphere), nameof(DysonSphere.grossRadius)))
                    //new CodeMatch(OpCodes.Ble_Un_S)
                ).Advance(1).SetOperandAndAdvance(start);

                return matcher.InstructionEnumeration();
            }

            /*[HarmonyTranspiler, HarmonyPatch(typeof(DysonSphere), "GameTick")]
            static IEnumerable<CodeInstruction> NodeComputeBufferPatch(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher matcher = new CodeMatcher(instructions, generator);

                // set the start of the for loop to gameTick % NODE_BUF_LOOP_INC + 1
                matcher.MatchForward(false,
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(DysonSphere), nameof(DysonSphere.nrdPool)))
                ).Advance(-4).SetOpcodeAndAdvance(OpCodes.Nop).InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Ldc_I8, (long)NODE_BUF_LOOP_INC),
                    new CodeInstruction(OpCodes.Rem),
                    new CodeInstruction(OpCodes.Conv_I4),
                    new CodeInstruction(OpCodes.Ldc_I4_1),
                    new CodeInstruction(OpCodes.Add)
                );

                // set the for loop to increment by NODE_BUF_LOOP_INC
                matcher.MatchForward(true,
                    new CodeMatch(OpCodes.Stfld, AccessTools.Field(typeof(DysonNodeRData), nameof(DysonNodeRData.layerRot)))
                ).Advance(2).SetInstructionAndAdvance(
                    new CodeInstruction(OpCodes.Ldc_I4, NODE_BUF_LOOP_INC)
                );

                return matcher.InstructionEnumeration();
            }*/
        }
    }
}
