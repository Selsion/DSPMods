using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DSPOptimizations
{
    class LowResShellsMultithreading
    {
        private static int numThreads;

        private static DysonShell[] jobPool;

        private static Dictionary<int, Vector3>[] vmaps;
        private static Dictionary<int, int>[] ivmaps;
        private static Dictionary<int, IntVector4>[] tmaps;

        private static int[][] vertsqOffsetArrs;

        public static void Init(BaseUnityPlugin plugin, Harmony harmony)
        {
            return; // TODO: as'lkdfjha;slkdkfj;alsdkfjh

            const int MAX_THREADS = MultithreadSystem.MAX_THREAD_COUNT;
            vmaps = new Dictionary<int, Vector3>[MAX_THREADS];
            ivmaps = new Dictionary<int, int>[MAX_THREADS];
            tmaps = new Dictionary<int, IntVector4>[MAX_THREADS];
            vertsqOffsetArrs = new int[MAX_THREADS][];

            harmony.PatchAll(typeof(Patch));
        }

        /** TODO:
         * transpiler for vanilla cp gen setup (before the p,q for loops)
         * initialize vertsqOffset for each thread
         * run the two for loops as a pseudo-one-dimension for loop with a strided threading approach
         * pool the different threads' vertsqOffset together, then finalize vertsqOffset
         */


        /** Alternative:
         * use the same method as before, except get rid of the allocations for vertsqOffset and nodecps
         * instead of vertsqOffset, use vertsqOffsetArrs[thread_id]
         * return from the method after counting all the vertices
         * aggregate all the vertsqOffset arrays into the final product separately
         */

        public static void VanillaCPCountsWrapper(DysonShell shell)
        {
            if(numThreads == 1)
            {
                LowResShells.Patch.GenerateVanillaCPCounts(shell);
                return;
            }

            int numNodes = shell.nodes.Count;
            for(int i = 0; i < numThreads; i++)
            {
                vertsqOffsetArrs[i] = new int[numNodes];
            }

            // TODO: make the transpiler use the correct vertsqOffset array
            // TODO: add the strided indexing
            // TODO: aggregate the arrays
            
        }

        class Patch
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(MultithreadSystem), nameof(MultithreadSystem.Init))]
            [HarmonyPatch(typeof(MultithreadSystem), nameof(MultithreadSystem.ResetUsedThreadCnt))]
            public static void SetThreadCount(MultithreadSystem __instance)
            {
                numThreads = __instance.usedThreadCnt;
            }


            // TODO: finish this, and maybe refactor the transpilers?
            [HarmonyReversePatch]
            [HarmonyPatch(typeof(DysonShell), "GenerateGeometry")]
            public static void ParallelVanillaCPCounts(DysonShell shell)
            {
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

                    // create and initialize a variable to store the number of nodes
                    LocalBuilder nodeCountVar = generator.DeclareLocal(typeof(int));
                    matcher.InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(DysonShell), nameof(DysonShell.nodes))),
                        new CodeInstruction(OpCodes.Callvirt, nodeCountCallOperand),//AccessTools.Property(typeof(List<DysonNode>), nameof(List<DysonNode>.Count))),
                                                                                    //new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(List<DysonNode>), nameof(List<DysonNode>.Count))),
                        new CodeInstruction(OpCodes.Stloc_S, nodeCountVar)
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
                    //matcher.Advance(7).CreateLabel(out Label vertsqOffsetAggBegin);
                    
                    // go back to the loop where we're adding a new vertex. remove the code that adds the vertex info to the dictionary
                    matcher.Start().Advance(newVertexPos).RemoveInstructions(9);
                    //  add a jump to the vertex processing code
                    matcher.InsertAndAdvance(
                        new CodeInstruction(OpCodes.Br, vertProcessBegin)
                    );
                    // add a jump to the vertsqOffset aggregating code after the for-loops
                    //matcher.Advance(14).InsertAndAdvance(new CodeInstruction(OpCodes.Br, vertsqOffsetAggBegin));
                    matcher.Advance(14).InsertAndAdvance(new CodeInstruction(OpCodes.Ret));

                    // add a return statement at the end of the vertsqOffset aggregating code
                    /*matcher.MatchForward(false,
                        new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(DysonShell), nameof(DysonShell._openListPrepare)))
                    ).Advance(-1).InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ret)
                    );*/

                    // replace all instances of the old node count variable with our new one
                    matcher.Start().MatchForward(true, new CodeMatch(OpCodes.Ldloc_S, oldNodeCountVarOperand)).Repeat(matcher2 =>
                       matcher2.SetOperandAndAdvance(nodeCountVar)
                    );

                    return matcher.InstructionEnumeration();
                }

                _ = Transpiler(null, null);
            }
        }  
    }
}
