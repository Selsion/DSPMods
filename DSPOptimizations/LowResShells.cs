using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace DSPOptimizations
{
    public class LowResShells
    {
        public static bool enabled = false; // TODO: what about mod saves when this is disabled?

        public static void Init(BaseUnityPlugin plugin, Harmony harmony)
        {
            enabled = plugin.Config.Bind<bool>("General", "LowResolutionShells", true,
                "Enables generating shell geometry with a lower resolution than normal. A layer's resolution is modified in the dyson sphere window."
                + " This will modify the way shells are stored in save files, but modded saves should still be compatible with the vanilla game."
                + " Additional save data will be stored in a file with the extension 'moddsv'.").Value;

            if (enabled)
            {
                harmony.PatchAll(typeof(Patch));
                LowResShellsUI.Init(plugin, harmony);
            }
        }

        public static void OnDestroy()
        {
            if (enabled)
                LowResShellsUI.OnDestroy();
        }

        private static void SwapVertOffsetArrays(DysonShell shell)
        {
            var temp = shell.vertsqOffset;
            shell.vertsqOffset = shell.vertsqOffset_lowRes;
            shell.vertsqOffset_lowRes = temp;
        }

        // convert an existing shell to have a different resolution. calls GenerateGeometry, but otherwise lightweight
        public static void RegenGeoLowRes(DysonShell shell)
        {
            int[] temp = (int[])shell.vertsqOffset.Clone();
            int[] oldNodeCps = (int[])shell.nodecps.Clone();
            shell.GenerateGeometry();
            shell.vertsqOffset_lowRes = shell.vertsqOffset;
            shell.vertsqOffset = temp;
            shell.nodecps = oldNodeCps;

            for (int nodeIndex = 0; nodeIndex < shell.nodes.Count; nodeIndex++)
            {
                int cps = shell.nodecps[nodeIndex];

                int cpHighRes = (shell.vertsqOffset[nodeIndex + 1] - shell.vertsqOffset[nodeIndex]) * 2;
                int cpLowRes = (shell.vertsqOffset_lowRes[nodeIndex + 1] - shell.vertsqOffset_lowRes[nodeIndex]) * 2;

                if (cpLowRes == 0)
                    continue;

                int cp_scaled = cps * cpLowRes;
                int nodecps_lowRes = cp_scaled / cpHighRes;

                for (int i = 0; i < nodecps_lowRes / 2; i++)
                {
                    int vertIdx = shell.vertsq[shell.vertsqOffset_lowRes[nodeIndex] + i];

                    if (i < nodecps_lowRes / 2 - 1)
                        shell.vertcps[vertIdx] = 2U;
                    else
                        shell.vertcps[vertIdx] = 2 - (uint)(nodecps_lowRes & 1);
                }
            }

            shell.buffer.SetData(shell.vertcps);
        }

        public class Patch
        {
            /** This is done to make the vanilla code export the low res version of vertsqOffset instead of the vanilla high res version.
             * This allows the low res shell to be loaded in the vanilla game. Only the node cp counts will be abnormal.
             * We swap the arrays back in the postfix patch.
             */

            [HarmonyPrefix]
            [HarmonyPatch(typeof(DysonShell), "Export")]
            public static bool ExportPrefix(DysonShell __instance)
            {
                SwapVertOffsetArrays(__instance);
                return true;
            }

            // swap back to undo what we did in the prefix patch
            [HarmonyPostfix]
            [HarmonyPatch(typeof(DysonShell), "Export")]
            public static void ExportPostfix(DysonShell __instance)
            {
                SwapVertOffsetArrays(__instance);
            }

            //
            [HarmonyTranspiler, HarmonyPatch(typeof(DysonShell), nameof(DysonShell.GenerateGeometry))]
            static IEnumerable<CodeInstruction> DysonShell_GenerateGeometry_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {

                CodeMatcher matcher = new CodeMatcher(instructions, generator);

                // find where we compute the value for the variable responsible for vertex density
                matcher.MatchForward(false,
                    new CodeMatch(OpCodes.Ldloc_S),
                    new CodeMatch(OpCodes.Ldc_R4, 1.2f),
                    new CodeMatch(OpCodes.Mul),
                    new CodeMatch(OpCodes.Ldc_R4, 80f)
                );

                // this should store the sphere's radius
                matcher.Advance(-13);
                OpCode radiusVarOpcode = matcher.Opcode;
                object radiusVarOperand = matcher.Operand;
                matcher.Advance(13);

                LocalBuilder scaleDownFactorVar = generator.DeclareLocal(typeof(float));

                // compute radius_lowRes as Math.Min(radius, parentLayer.radius_lowRes)
                // TODO: this shouldn't even be needed anymore since we handle that with the layer
                matcher.InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldarg_0),

                    //new CodeInstruction(radiusVarOpcode, radiusVarOperand),

                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(DysonShell), nameof(DysonShell.parentLayer))),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(DysonSphereLayer), nameof(DysonSphereLayer.radius_lowRes))),

                    //new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Math), nameof(Math.Min), new[] { typeof(float), typeof(float) })),
                    new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(DysonShell), nameof(DysonShell.radius_lowRes)))
                );

                // compute scaleDownFactor as radius_lowRes / radius
                matcher.InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(DysonShell), nameof(DysonShell.radius_lowRes))),
                    new CodeInstruction(radiusVarOpcode, radiusVarOperand),
                    new CodeInstruction(OpCodes.Div),
                    new CodeInstruction(OpCodes.Stloc_S, scaleDownFactorVar)
                );

                // scale radius down to decrease the number of vertices
                matcher.Advance(1).InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldloc_S, scaleDownFactorVar),
                    new CodeInstruction(OpCodes.Mul)
                );

                // scale up vertex position parameters to offset the decreased vertex count
                matcher.MatchForward(true,
                    new CodeMatch(OpCodes.Ldloc_S),
                    new CodeMatch(OpCodes.Conv_R8),
                    new CodeMatch(OpCodes.Ldloc_S),
                    new CodeMatch(OpCodes.Conv_R8),
                    new CodeMatch(OpCodes.Ldc_R8, 0.5),
                    new CodeMatch(OpCodes.Mul),
                    new CodeMatch(OpCodes.Sub)
                ).Advance(1).InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldloc_S, scaleDownFactorVar),
                    new CodeInstruction(OpCodes.Div)
                ).Advance(5).InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldloc_S, scaleDownFactorVar),
                    new CodeInstruction(OpCodes.Div)
                );

                return matcher.InstructionEnumeration();
            }

            [HarmonyReversePatch]
            [HarmonyPatch(typeof(DysonShell), "GenerateGeometry")]
            public static void GenerateVanillaCPCounts(DysonShell shell)
            {
                //NoShellDataPatch.genGeoVanillaCounts(ref shell);
                //return;

                IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
                {
                    CodeMatcher matcher = new CodeMatcher(instructions, generator);

                    // find the local variable originally used to store the node count
                    matcher.MatchForward(true,
                        new CodeMatch(OpCodes.Ldarg_0),
                        new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(DysonShell), nameof(DysonShell.nodes))),
                        new CodeMatch(OpCodes.Callvirt),//, AccessTools.Property(typeof(List<DysonNode>), nameof(List<DysonNode>.Count))),
                        new CodeMatch(OpCodes.Stloc_S)
                    );
                    object nodeCountCallOperand = matcher.Advance(-1).Operand;
                    object oldNodeCountVarOperand = matcher.Advance(1).Operand;

                    // remove the code that initializes the dictionaries - we don't need them
                    matcher.Start().MatchForward(false,
                        new CodeMatch(OpCodes.Ldsfld, AccessTools.Field(typeof(DysonShell), nameof(DysonShell.s_vmap)))
                    ).SetInstructionAndAdvance(new CodeInstruction(OpCodes.Nop)) // set the first instruction to nop in case of a jump
                    .RemoveInstructions(20);

                    // create and initialize variables to store the number of vertices and nodes
                    LocalBuilder vertexCountVar = generator.DeclareLocal(typeof(int));
                    LocalBuilder nodeCountVar = generator.DeclareLocal(typeof(int));
                    matcher.InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldc_I4_0),
                        new CodeInstruction(OpCodes.Stloc_S, vertexCountVar),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(DysonShell), nameof(DysonShell.nodes))),
                        new CodeInstruction(OpCodes.Callvirt, nodeCountCallOperand),//AccessTools.Property(typeof(List<DysonNode>), nameof(List<DysonNode>.Count))),
                                                                                    //new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(List<DysonNode>), nameof(List<DysonNode>.Count))),
                        new CodeInstruction(OpCodes.Stloc_S, nodeCountVar)
                    );

                    // initialize the vertsqOffset and nodecps arrays
                    matcher.InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldloc_S, nodeCountVar),
                        new CodeInstruction(OpCodes.Ldc_I4_1),
                        new CodeInstruction(OpCodes.Add),
                        new CodeInstruction(OpCodes.Newarr, typeof(int)),
                        new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(DysonShell), nameof(DysonShell.vertsqOffset))),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldloc_S, nodeCountVar),
                        new CodeInstruction(OpCodes.Ldc_I4_1),
                        new CodeInstruction(OpCodes.Add),
                        new CodeInstruction(OpCodes.Newarr, typeof(int)),
                        new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(DysonShell), nameof(DysonShell.nodecps)))
                    );

                    // find the two for-loop variables for making vertices. these are the "pqArr" entries "p" and "q"
                    matcher.Advance(2);
                    object outerLoopVarOperand = matcher.Operand;
                    matcher.Advance(4);
                    object innerLoopVarOperand = matcher.Operand;

                    // find the local variable that stores the current vertex's position
                    // note: this commented code extracted the wrong variable
                    /*matcher.MatchForward(true,
                        new CodeMatch(OpCodes.Ldloc_S),
                        new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(VectorLF3), nameof(VectorLF3.z))),
                        new CodeMatch(OpCodes.Ldloc_S),
                        new CodeMatch(OpCodes.Ldloc_S),
                        new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(VectorLF3), nameof(VectorLF3.z))),
                        new CodeMatch(OpCodes.Mul),
                        new CodeMatch(OpCodes.Add),
                        new CodeMatch(OpCodes.Newobj),
                        new CodeMatch(OpCodes.Stloc_S),
                        new CodeMatch(OpCodes.Ldloca_S),
                        new CodeMatch(OpCodes.Call),//, AccessTools.Property(typeof(VectorLF3), nameof(VectorLF3.normalized))),
                        new CodeMatch(OpCodes.Stloc_S)
                    );*/
                    matcher.MatchForward(true, new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Maths), nameof(Maths.RotateLF)))).Advance(1);
                    object vertexPosVarOperand = matcher.Operand;

                    // find the code in the for-loop where we add the new vertex to the vertex dictionary
                    matcher.MatchForward(false,
                        new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(DysonShell), nameof(DysonShell._get_key)))
                    ).Advance(-2);
                    int newVertexPos = matcher.Pos;
                    Label loopEnd = (Label)matcher.Advance(-1).Operand;
                    object vecCastOperand = matcher.Advance(8).Operand;


                    // find the code where we process the vertices and increment vertsqOffset
                    /*matcher.MatchForward(false,
                        new CodeMatch(OpCodes.Ldarg_0),
                        new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(DysonShell), nameof(DysonShell.verts))),
                        new CodeMatch(OpCodes.Ldloc_S),
                        new CodeMatch(OpCodes.Ldelem)//, typeof(UnityEngine.Vector3)), // TO DEBUG: for some reason, adding this opcode to the code match will result in no matches found (index out of range error later)
                        new CodeMatch(OpCodes.Stloc_S)
                    );*/
                    matcher.MatchForward(false,
                        //new CodeMatch(OpCodes.Ldc_R8, double.MaxValue)
                        new CodeMatch(OpCodes.Ldc_I4, 479001600)
                    ).Advance(-5).Advance(-17); // TODO: switch back to the original code above. this was used for debugging
                    // replace verts[idx] with the vertex pos from the local variable found above
                    matcher.Insert(new CodeInstruction(OpCodes.Ldloc_S, vertexPosVarOperand)).CreateLabel(out Label vertProcessBegin);
                    matcher.Advance(1)
                    .SetOpcodeAndAdvance(OpCodes.Nop) // there's a label here
                    .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Call, vecCastOperand))
                    .RemoveInstructions(2);

                    // replace the pqArr elements with the for-loop variables found above
                    ///*
                    matcher.Advance(7).RemoveInstructions(10).InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldloc_S, outerLoopVarOperand),
                        new CodeInstruction(OpCodes.Ldloc_S, innerLoopVarOperand)
                    );///*
                    // replace loading the local variable equal to nodeCount / 2 with nodeCount / 2
                    matcher.Advance(2).RemoveInstruction().InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldloc_S, nodeCountVar),
                        new CodeInstruction(OpCodes.Ldc_I4_2),
                        new CodeInstruction(OpCodes.Div)
                    );
                    // find and remove the line of code that assigns an element of vertAttr after the for-loop
                    matcher.MatchForward(false,
                        new CodeMatch(OpCodes.Ldarg_0),
                        new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(DysonShell), nameof(DysonShell.vertAttr))),
                        new CodeMatch(OpCodes.Ldloc_S),
                        new CodeMatch(OpCodes.Ldloc_S),
                        new CodeMatch(OpCodes.Stelem_I4)
                    ).SetOpcodeAndAdvance(OpCodes.Nop).RemoveInstructions(4);
                    // add a jump back to our original code after incrementing vertsqOffset
                    matcher.Advance(9).InsertAndAdvance(new CodeInstruction(OpCodes.Br, loopEnd));
                    // create a label for the beginning of the vertsqOffset aggregating code, which is right after the for-loop
                    matcher.Advance(7).CreateLabel(out Label vertsqOffsetAggBegin);
                    ///*
                    // go back to the loop where we're adding a new vertex. remove the code that adds the vertex info to the dictionary
                    matcher.Start().Advance(newVertexPos).RemoveInstructions(9);
                    // increment the vertex count, then add a jump to the vertex processing code
                    matcher.InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldloc_S, vertexCountVar),
                        new CodeInstruction(OpCodes.Ldc_I4_1),
                        new CodeInstruction(OpCodes.Add),
                        new CodeInstruction(OpCodes.Stloc_S, vertexCountVar),
                        new CodeInstruction(OpCodes.Br, vertProcessBegin)
                    );
                    // add a jump to the vertsqOffset aggregating code after the for-loops
                    matcher.Advance(14).InsertAndAdvance(new CodeInstruction(OpCodes.Br, vertsqOffsetAggBegin));

                    // add a return statement at the end of the vertsqOffset aggregating code
                    matcher.MatchForward(false,
                        new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(DysonShell), nameof(DysonShell._openListPrepare)))
                    ).Advance(-1).InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ret)
                    );

                    // replace all instances of the old node count variable with our new one
                    matcher.Start().MatchForward(true, new CodeMatch(OpCodes.Ldloc_S, oldNodeCountVarOperand)).Repeat(matcher2 =>
                       matcher2.SetOperandAndAdvance(nodeCountVar)
                    );//*/

                    return matcher.InstructionEnumeration();
                }

                _ = Transpiler(null, null);
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(DysonShell), "GenerateGeometry")]
            public static void DysonShell_GenerateGeometry_Prefix(ref DysonShell __instance, ref bool __state)
            {
                __state = __instance.vertsqOffset == null;
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(DysonShell), "GenerateGeometry")]
            public static void DysonShell_GenerateGeometry_Postfix(ref DysonShell __instance, ref bool __state)
            {
                if (__state) // skip generating vanilla cp counts if we're regenerating the shell
                {
                    __instance.vertsqOffset_lowRes = (int[])__instance.vertsqOffset.Clone();
                    //NoShellDataPatch.genGeoVanillaCounts(ref __instance);
                    if(__instance.radius_lowRes != __instance.parentLayer.orbitRadius)
                        GenerateVanillaCPCounts(__instance);
                }
            }

            /*[HarmonyPrefix]
            [HarmonyPatch(typeof(DysonShell), "GenerateGeometry")]
            public static void DysonShell_GenerateGeometry_Prefix(ref DysonShell __instance, ref int[] __state)
            {
                if (__instance.vertsqOffset == null)
                    __state = null;
                else
                    __state = (int[])__instance.vertsqOffset.Clone();
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(DysonShell), "GenerateGeometry")]
            public static void DysonShell_GenerateGeometry_Postfix(ref DysonShell __instance, ref int[] __state)
            {
                __instance.vertsqOffset_lowRes = (int[])__instance.vertsqOffset.Clone();
                if (__state == null && __instance.radius_lowRes != __instance.parentLayer.orbitRadius)
                    GenerateVanillaCPCounts(__instance);
            }*/

            [HarmonyTranspiler, HarmonyPatch(typeof(DysonSphereLayer), nameof(DysonSphereLayer.RemoveDysonShell))]
            static IEnumerable<CodeInstruction> DysonSphereLayer_RemoveDysonShell_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher matcher = new CodeMatcher(instructions, generator);

                LocalBuilder nodeCountVar = generator.DeclareLocal(typeof(int));
                LocalBuilder nodeCpsVar = generator.DeclareLocal(typeof(int));

                // find the shell local variable
                matcher.MatchForward(false,
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(DysonShell), nameof(DysonShell.vertcps))),
                    new CodeMatch(OpCodes.Ldloc_S),
                    new CodeMatch(OpCodes.Ldelem_U4),
                    new CodeMatch(OpCodes.Conv_R_Un),
                    new CodeMatch(OpCodes.Conv_R4),
                    new CodeMatch(OpCodes.Ldc_R4, 0.5f)
                ).Advance(-1);

                OpCode shellLocalVarOpCode = matcher.Opcode;
                object shellLocalVarOperand = matcher.Operand;

                // init the nodeCount local variable
                matcher.Advance(-3).InsertAndAdvance(
                        new CodeInstruction(shellLocalVarOpCode, shellLocalVarOperand),
                        new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(DysonShell), nameof(DysonShell.nodes))),
                        new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(List<DysonNode>), "get_Count")),
                        new CodeInstruction(OpCodes.Stloc_S, nodeCountVar)
                    );

                // store the info for the outer for-loop variable
                matcher.Advance(5);
                OpCode idxVarOpCode = matcher.Opcode;
                object idxVarOperand = matcher.Operand;

                // remove the old vertex count check. update the nodeCps variable
                matcher.Advance(-2).SetOpcodeAndAdvance(OpCodes.Nop).RemoveInstructions(7).InsertAndAdvance(
                //dysonShell.nodecps[i] / 2;
                    new CodeInstruction(shellLocalVarOpCode, shellLocalVarOperand),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(DysonShell), nameof(DysonShell.nodecps))),
                    new CodeInstruction(idxVarOpCode, idxVarOperand),
                    new CodeInstruction(OpCodes.Ldelem_I4),
                    new CodeInstruction(OpCodes.Ldc_I4_2),
                    new CodeInstruction(OpCodes.Div),
                    new CodeInstruction(OpCodes.Stloc_S, nodeCpsVar)
                );

                // change the sail position to the node's position rather than the vertex position
                // dysonShell.nodes[i].pos
                matcher.Advance(4).SetOperandAndAdvance(
                    AccessTools.Field(typeof(DysonShell), nameof(DysonShell.nodes))
                ).Advance(1).SetInstructionAndAdvance(
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(List<DysonNode>), "get_Item"))
                ).InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(DysonNode), nameof(DysonNode.pos)))
                );


                // modify the inner and outer for-loops to iterate until nodeCps and nodeCount, respectively
                matcher.MatchForward(true,
                    new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(DysonSwarm), nameof(DysonSwarm.AddSolarSail)))
                ).Advance(7).SetInstruction(
                    new CodeInstruction(OpCodes.Ldloc_S, nodeCpsVar)
                ).Advance(7).SetInstructionAndAdvance(
                    new CodeInstruction(OpCodes.Ldloc_S, nodeCountVar)
                ).RemoveInstruction();

                return matcher.InstructionEnumeration();
            }

            [HarmonyTranspiler, HarmonyPatch(typeof(DysonShell), nameof(DysonShell.Construct))]
            static IEnumerable<CodeInstruction> DysonShell_Construct_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher matcher = new CodeMatcher(instructions, generator);

                // remove the original code that increments the current vertex
                matcher.MatchForward(false,
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(DysonShell), nameof(DysonShell.vertsq)))
                ).SetOpcodeAndAdvance(OpCodes.Nop).RemoveInstructions(13)
                // add our code that increments the current vertex only at the correct cp count
                .InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldarg_1),
                    Transpilers.EmitDelegate<Action<DysonShell, int>>((DysonShell shell, int nodeIndex) =>
                    {
                        if (DSPGame.IsMenuDemo)
                            return;

                        int cpHighRes = (shell.vertsqOffset[nodeIndex + 1] - shell.vertsqOffset[nodeIndex]) * 2;
                        int cpLowRes = (shell.vertsqOffset_lowRes[nodeIndex + 1] - shell.vertsqOffset_lowRes[nodeIndex]) * 2;
                        int cp_scaled = shell.nodecps[nodeIndex] * cpLowRes;

                        //if (cpLowRes == 0) // used to divide by cpLowRes. no need to check this anymore
                        //return;
                        if (shell.vertexCount == 0) // TODO: legacy code for old test save. delete this
                            return;

                        if (cp_scaled % cpHighRes >= cpHighRes - cpLowRes)
                        {
                            //int num4_2 = __instance.vertsqOffset_lowRes[nodeIndex] + __instance.nodecps[nodeIndex] / 2 / rep; // do we divide by 2??
                            int num4_2 = shell.vertsqOffset_lowRes[nodeIndex] + cp_scaled / cpHighRes / 2;
                            int num5 = shell.vertsq[num4_2];
                            shell.vertcps[num5] += 1U;
                            shell.buffer.SetData(shell.vertcps); // note: added this. check buffer with legacy code. is it always non-null here?
                            Assert.True(shell.vertcps[num5] <= 2U);
                        }
                    })
                );

                // remove assertion at the end
                matcher.MatchForward(false,
                    new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Assert), nameof(Assert.True), new[] { typeof(bool) }))
                ).Advance(-13).RemoveInstructions(14); // also remove the buffer call
                //).Advance(-8).RemoveInstructions(9);

                return matcher.InstructionEnumeration();
            }

            [HarmonyTranspiler, HarmonyPatch(typeof(DysonSphereSegmentRenderer), nameof(DysonSphereSegmentRenderer.DrawModels))]
            static IEnumerable<CodeInstruction> DysonSphereSegmentRenderer_DrawModels_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher matcher = new CodeMatcher(instructions, generator);

                matcher.MatchForward(false, new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(DysonSphereLayer), nameof(DysonSphereLayer.shellPool))));
                matcher.Advance(4);
                OpCode shellOpCode = matcher.Opcode;
                var shellOperand = matcher.Operand;
                matcher.Advance(5);
                var label = matcher.Operand;

                matcher.Advance(1).InsertAndAdvance(
                    new CodeInstruction(shellOpCode, shellOperand),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(DysonShell), nameof(DysonShell.vertexCount))),
                    new CodeInstruction(OpCodes.Ldc_I4_2),
                    new CodeInstruction(OpCodes.Ble, label)
                );

                return matcher.InstructionEnumeration();
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(DysonSphere), "AddLayer")]
            public static void DysonSphere_AddLayer_Postfix(DysonSphereLayer __result)
            {
                __result.radius_lowRes = __result.orbitRadius;
            }
        }
    }
}
