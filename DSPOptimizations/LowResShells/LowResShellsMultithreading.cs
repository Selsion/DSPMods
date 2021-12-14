using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

using System.IO;

namespace DSPOptimizations
{
    public class LowResShellsMultithreading
    {
        private static int numThreads;

        private static AutoResetEvent[] vcpCompleteEvents;
        private static Dictionary<int, int> threadIdToId;
        private static int nextThreadId;

        /*private static DysonShell[] jobPool;

        private static Dictionary<int, Vector3>[] vmaps;
        private static Dictionary<int, int>[] ivmaps;
        private static Dictionary<int, IntVector4>[] tmaps;*/

        private static int[][] vertsqOffsetArrs;

        private static DysonShell currentShell;

        public static void Init(BaseUnityPlugin plugin, Harmony harmony)
        {
            const int MAX_THREADS = MultithreadSystem.MAX_THREAD_COUNT;
            /*vmaps = new Dictionary<int, Vector3>[MAX_THREADS];
            ivmaps = new Dictionary<int, int>[MAX_THREADS];
            tmaps = new Dictionary<int, IntVector4>[MAX_THREADS];*/
            vertsqOffsetArrs = new int[MAX_THREADS][];

            harmony.PatchAll(typeof(Patch));

            vcpCompleteEvents = new AutoResetEvent[MAX_THREADS];

            for (int i = 0; i < MAX_THREADS; i++)
            {
                vcpCompleteEvents[i] = new AutoResetEvent(true);
            }
        }

        public static void VanillaCPCountsWrapper(DysonShell shell)
        {
            if(numThreads <= 1) // note: the game usesa value of 0 for 1 thread
            {
                LowResShells.Patch.GenerateVanillaCPCounts(shell);
                return;
            }

            currentShell = shell;

            int numNodes = shell.nodes.Count;
            threadIdToId = new Dictionary<int, int>();
            nextThreadId = 0;
            for (int i = 0; i < numThreads; i++)
            {
                vertsqOffsetArrs[i] = new int[numNodes + 1];
                vcpCompleteEvents[i].Reset();
            }

            for (int i = 0; i < numThreads; i++)
                ThreadPool.QueueUserWorkItem(new WaitCallback(RunThread));

            for (int i = 0; i < numThreads; i++)
                vcpCompleteEvents[i].WaitOne();

            // note: shell.vertsqOffset can be null at this point // TODO: find out why, and if other things can be null
            /*for (int i = 0; i < vertsqOffsetArrs[0].Length; i++)
                for (int j = 0; j < numThreads; j++)
                    UnityEngine.Debug.Log("i:" + i + " j:" + j + " v:" + vertsqOffsetArrs[j][i]);*/

            shell.vertsqOffset = new int[numNodes + 1];

            for (int i = 0; i < shell.vertsqOffset.Length; i++)
                for (int j = 0; j < numThreads; j++)
                    shell.vertsqOffset[i] += vertsqOffsetArrs[j][i];

            int numVerts = 0;
            for (int i = 0; i < shell.vertsqOffset.Length; i++)
                numVerts += shell.vertsqOffset[i];
            for (int i = shell.vertsqOffset.Length - 1; i >= 0; i--)
            {
                shell.vertsqOffset[i] = numVerts;
                if (i > 0)
                    numVerts -= shell.vertsqOffset[i - 1];
            }
            Assert.Zero(numVerts);
        }

        private static void RunThread(object state = null)
        {
            int mappedThreadId = -1;
            lock (threadIdToId) // TODO: this is gross. find another way to get the thread IDs
            {
                mappedThreadId = nextThreadId++; // apparently it reuses threads?
                threadIdToId[Thread.CurrentThread.ManagedThreadId] = mappedThreadId;
            }

            Patch.ParallelVanillaCPCounts(currentShell);
            vcpCompleteEvents[mappedThreadId].Set();
        }

        private struct IndexingInfo
        {
            public int startIdx, endIdx, loopWidth, threadID;
        }

        private static IndexingInfo GetThreadIndexingInfo(int num9)
        {
            IndexingInfo ret;

            // pseudo-2d: each 1d loop is from -num9 to num9 inclusive
            //      (num9*2+1)^2 = 4*num9^2 + 4*num9 + 1 indices
            //      split these among <numThread> threads
            ret.loopWidth = num9 * 2 + 1;
            int numTasks = ret.loopWidth * ret.loopWidth;
            int chunkSize = (numTasks - 1) / numThreads + 1; // rounds up

            ret.threadID = threadIdToId[Thread.CurrentThread.ManagedThreadId];

            ret.startIdx = chunkSize * ret.threadID;
            ret.endIdx = Math.Min(ret.startIdx + chunkSize, numTasks);

            //DSPOptimizations.logger.LogInfo(string.Format("threadID:{0} start:{1} end:{2} loopWidth:{3} num9:{4}", ret.threadID, ret.startIdx, ret.endIdx, ret.loopWidth, num9));

            return ret;
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
                        new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(List<DysonNode>), "get_Count")),
                        new CodeMatch(OpCodes.Stloc_S)
                    );
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
                        new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(List<DysonNode>), "get_Count")),
                        new CodeInstruction(OpCodes.Stloc_S, nodeCountVar)
                    );

                    var num9Operand = matcher.Operand;
                    // we're just before the for loop. get the indexing info
                    LocalBuilder indexingInfo = generator.DeclareLocal(typeof(IndexingInfo));
                    matcher.Advance(1).InsertAndAdvance(
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(LowResShellsMultithreading), nameof(LowResShellsMultithreading.GetThreadIndexingInfo))),
                        new CodeInstruction(OpCodes.Stloc_S, indexingInfo)
                    );
                    // we're using the ldloc call for num9 above. remove the negate for the start index and replace with our new start
                    matcher.RemoveInstruction().InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldloc_S, indexingInfo),
                        new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(IndexingInfo), nameof(IndexingInfo.startIdx)))
                    );
                    object loopVarOperand = matcher.Operand;
                    matcher.Advance(2).SetOpcodeAndAdvance(OpCodes.Nop).RemoveInstructions(3);

                    // set p and q based on the pseudo-2d index
                    LocalBuilder pVar = generator.DeclareLocal(typeof(int));
                    LocalBuilder qVar = generator.DeclareLocal(typeof(int));
                    matcher.InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldloc_S, loopVarOperand),
                        new CodeInstruction(OpCodes.Ldloc_S, indexingInfo),
                        new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(IndexingInfo), nameof(IndexingInfo.loopWidth))),
                        new CodeInstruction(OpCodes.Div),
                        new CodeInstruction(OpCodes.Ldloc_S, num9Operand),
                        new CodeInstruction(OpCodes.Sub),
                        new CodeInstruction(OpCodes.Stloc_S, pVar),
                        new CodeInstruction(OpCodes.Ldloc_S, loopVarOperand),
                        new CodeInstruction(OpCodes.Ldloc_S, indexingInfo),
                        new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(IndexingInfo), nameof(IndexingInfo.loopWidth))),
                        new CodeInstruction(OpCodes.Rem),
                        new CodeInstruction(OpCodes.Ldloc_S, num9Operand),
                        new CodeInstruction(OpCodes.Sub),
                        new CodeInstruction(OpCodes.Stloc_S, qVar)
                    );
                    // replace the for loop variables with pVar and qVar
                    matcher.SetOperandAndAdvance(pVar).Advance(1).SetOperandAndAdvance(qVar).Advance(5).SetOperandAndAdvance(qVar);

                    // find the two for-loop variables for making vertices. these are the "pqArr" entries "p" and "q"
                    /*matcher.Advance(2);
                    object outerLoopVarOperand = matcher.Operand;
                    matcher.Advance(4);
                    object innerLoopVarOperand = matcher.Operand;*/

                    // find the local variable that stores the current vertex's position
                    matcher.MatchForward(true, new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Maths), nameof(Maths.RotateLF)))).Advance(1);
                    object vertexPosVarOperand = matcher.Operand;

                    // find the code in the for-loop where we add the new vertex to the vertex dictionary
                    matcher.MatchForward(false,
                        new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(DysonShell), nameof(DysonShell._get_key)))
                    ).Advance(-2);
                    int newVertexPos = matcher.Pos;
                    Label loopEnd = (Label)matcher.Advance(-1).Operand;
                    object vecCastOperand = matcher.Advance(8).Operand;

                    // fix the end of the for loop to be 1d and end at the right index
                    matcher.Advance(2).SetOpcodeAndAdvance(OpCodes.Nop).RemoveInstructions(6).Advance(5);
                    matcher.SetOperandAndAdvance(indexingInfo).InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(IndexingInfo), nameof(IndexingInfo.endIdx)))
                    ).SetOpcodeAndAdvance(OpCodes.Blt); // note: it's normally Ble, but we want to end before endIdx otherwise we double-count that vertex

                    //return matcher.InstructionEnumeration();

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
                    matcher.SetOpcodeAndAdvance(OpCodes.Nop); // there's a label here
                    matcher.Insert(new CodeInstruction(OpCodes.Ldloc_S, vertexPosVarOperand)).CreateLabel(out Label vertProcessBegin);
                    matcher.Advance(1)
                    .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Call, vecCastOperand))
                    .RemoveInstructions(2);

                    //return matcher.InstructionEnumeration();

                    // replace the pqArr elements with the for-loop variables found above
                    ///*
                    matcher.Advance(7).RemoveInstructions(10).InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldloc_S, pVar),
                        new CodeInstruction(OpCodes.Ldloc_S, qVar)
                    );///*
                    //return matcher.InstructionEnumeration();
                    // replace loading the local variable equal to nodeCount / 2 with nodeCount / 2
                    matcher.Advance(20).RemoveInstruction().InsertAndAdvance(
                    //matcher.Advance(2).RemoveInstruction().InsertAndAdvance( // TODO: how did this error show up? don't we use this code elsewhere?
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

                    // replace shell.vertsqOffset with vertsqOffsetArrs[indexingInfo.threadId]
                    matcher.RemoveInstructions(2).InsertAndAdvance(
                        new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(LowResShellsMultithreading), nameof(vertsqOffsetArrs))),
                        new CodeInstruction(OpCodes.Ldloc_S, indexingInfo),
                        new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(IndexingInfo), nameof(IndexingInfo.threadID))),
                        new CodeInstruction(OpCodes.Ldelem_Ref)
                    );

                    // add a jump back to our original code after incrementing vertsqOffset
                    matcher.Advance(11).InsertAndAdvance(new CodeInstruction(OpCodes.Br, loopEnd));
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
                    matcher.Advance(9).InsertAndAdvance(new CodeInstruction(OpCodes.Ret));

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
