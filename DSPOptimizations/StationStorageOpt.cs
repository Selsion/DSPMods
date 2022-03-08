using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DSPOptimizations
{
    [RunPatches(typeof(Patch))]
    [Optimization("StationStorageOpt", "Adds multithreading to station storage logic", false, new Type[] { })]
    class StationStorageOpt : OptimizationSet
    {
        private static int[] stationIdMap;
        private static int[] factorySizes;
        private static int[] factorySizesPrefixSum;

        private static int numThreads;
        private static WorkerThread[] threads;

        public override void Init(BaseUnityPlugin plugin)
        {
            threads = new WorkerThread[MultithreadSystem.MAX_THREAD_COUNT];
            for (int i = 0; i < MultithreadSystem.MAX_THREAD_COUNT; i++)
                threads[i] = new WorkerThread(i);
        }

        private static void InitFactoryInfo(GameData data)
        {
            int numFactories = data.factories.Length;
            factorySizes = new int[numFactories];
            factorySizesPrefixSum = new int[numFactories];
            stationIdMap = new int[0];
        }

        private static void RunStorageLogic(bool isInput)
        {
            for (int i = 0; i < numThreads; i++)
            {
                threads[i].completeEvent.Reset();
                ThreadPool.QueueUserWorkItem(threads[i].callback, isInput);
            }

            for (int i = 0; i < numThreads; i++) {
                threads[i].completeEvent.WaitOne();
            }
        }

        class WorkerThread
        {
            private int id;
            public WaitCallback callback;
            public AutoResetEvent completeEvent;

            public WorkerThread(int id)
            {
                this.id = id;
                callback = new WaitCallback(ThreadCallback);
                completeEvent = new AutoResetEvent(true);
            }

            private void ThreadCallback(object state = null)
            {   
                bool isInput = (bool)state;

                int chunkSize = (stationIdMap.Length - 1) / numThreads + 1; // rounds up
                int startIdx = chunkSize * id;
                int endIdx = Math.Min(startIdx + chunkSize, stationIdMap.Length);
                                                            
                for (int i = startIdx; i < endIdx; i++)
                {
                    int flattenedId = stationIdMap[i];
                    /*int factoryIdx = Array.BinarySearch(factorySizesPrefixSum, flattenedId + 1);
                    if (factoryIdx < 0)
                        factoryIdx = ~factoryIdx;*/
                    int factoryIdx = Utils.LowerBound(factorySizesPrefixSum, flattenedId + 1, 0, GameMain.data.factoryCount);
                    int localStationIdx = flattenedId - (factoryIdx > 0 ? factorySizesPrefixSum[factoryIdx - 1] : 0);

                    //Plugin.logger.LogInfo(string.Format("thread {0} at {1} from {2} to {3}. flattenedId={4}, factoryIdx={5}, localStationIdx={6}", id, i, startIdx, endIdx, flattenedId, factoryIdx, localStationIdx));

                    var factory = GameMain.data.factories[factoryIdx];
                    var station = factory.transport.stationPool[localStationIdx];

                    if(station != null && station.id == localStationIdx)
                    {
                        CargoTraffic cargoTraffic = factory.cargoTraffic;
                        SignData[] entitySignPool = factory.entitySignPool;

                        if (isInput)
                            station.UpdateInputSlots(cargoTraffic, entitySignPool);
                        else
                            station.UpdateOutputSlots(cargoTraffic, entitySignPool, GameMain.history.stationPilerLevel);
                    }
                }

                completeEvent.Set();
            }
        }
        
        class Patch
        {
            [HarmonyPrefix, HarmonyPatch(typeof(PlanetTransport), nameof(PlanetTransport.SetStationCapacity))]
            public static void SetStationCapacityPostfix(PlanetTransport __instance, int newCapacity)
            {
                int factoryIdx = __instance.factory.index;
                // check for an invalid factory index for compatibility with the BlackBox mod
                if (factoryIdx < 0 || factoryIdx >= factorySizes.Length)
                    return;

                int newTotalSize = stationIdMap.Length + newCapacity - __instance.stationCapacity;
                stationIdMap = new int[newTotalSize];
                for (int i = 0; i < newTotalSize; i++)
                    stationIdMap[i] = i;
                stationIdMap.Shuffle();

                
                int factoryCount = GameMain.data.factoryCount;
                factorySizes[factoryIdx] = newCapacity;
                int maxIdx = Math.Min(factoryCount + 1, factorySizes.Length);
                for (int i = factoryIdx; i < maxIdx; i++) // add 1 in case a new factory was added. the count wouldn't be updated yet
                    factorySizesPrefixSum[i] = (i > 0 ? factorySizesPrefixSum[i - 1] : 0) + factorySizes[i];
            }

            /*[HarmonyPrefix, HarmonyPatch(typeof(GameData), nameof(GameData.NewGame))]
            public static void InitIdMapPatch1(GameDesc _gameDesc)
            {
                factorySizes = new int[_gameDesc.starCount * 6];
                factorySizesPrefixSum = new int[_gameDesc.starCount * 6];
                stationIdMap = new int[0];
            }

            [HarmonyPostfix, HarmonyPatch(typeof(GameDesc), nameof(GameDesc.Import))]
            public static void InitIdMapPatch2(GameDesc __instance)
            {
                factorySizes = new int[__instance.starCount * 6];
                factorySizesPrefixSum = new int[__instance.starCount * 6];
                stationIdMap = new int[0];
            }*/

            /*[HarmonyPostfix]
            [HarmonyPatch(typeof(GalacticTransport), nameof(GalacticTransport.Arragement))]
            [HarmonyPatch(typeof(GalacticTransport), nameof(GalacticTransport.Init))]
            public static void ShuffleMapPostfix(GalacticTransport __instance)
            {
                stationIdMap.Shuffle(__instance.stationCursor);
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(GalacticTransport), nameof(GalacticTransport.AddStationComponent))]
            public static void AddStationPrefix(GalacticTransport __instance, ref int __state)
            {
                __state = __instance.stationCapacity;
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(GalacticTransport), nameof(GalacticTransport.AddStationComponent))]
            public static void AddStationPostfix(GalacticTransport __instance, ref int __state)
            {
                if (__state != __instance.stationCapacity)
                    stationIdMap.Shuffle(__instance.stationCursor);
                else
                    stationIdMap.RandomPrefixSwap(__instance.stationCursor - 1);
            }*/

            private static void ReplaceCall(CodeMatcher matcher, bool isInput)
            {
                string methodName = isInput ? nameof(PlanetTransport.GameTick_InputFromBelt) : nameof(PlanetTransport.GameTick_OutputToBelt);

                matcher.MatchForward(false,
                    new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(PlanetTransport), methodName))
                ).Advance(-14)
                .RemoveInstructions(3)
                .SetOpcodeAndAdvance(OpCodes.Nop)
                .RemoveInstructions(11)
                .SetOpcodeAndAdvance(OpCodes.Nop)
                .RemoveInstructions(3)
                .SetOpcodeAndAdvance(OpCodes.Nop)
                .RemoveInstructions(3)
                .InsertAndAdvance(
                    new CodeInstruction(isInput ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StationStorageOpt), nameof(StationStorageOpt.RunStorageLogic)))
                );
            }

            [HarmonyTranspiler, HarmonyPatch(typeof(GameData), "GameTick")]
            static IEnumerable<CodeInstruction> StorageCallSkipPatch(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher matcher = new CodeMatcher(instructions, generator);
                ReplaceCall(matcher, true);
                ReplaceCall(matcher, false);
                return matcher.InstructionEnumeration();
            }

            [HarmonyTranspiler]
            [HarmonyPatch(typeof(GameData), "NewGame")]
            [HarmonyPatch(typeof(GameData), "Import")]
            static IEnumerable<CodeInstruction> InitFactoryInfoPatch(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher matcher = new CodeMatcher(instructions, generator);

                matcher.MatchForward(true,
                    new CodeMatch(OpCodes.Ldc_I4_6),
                    new CodeMatch(OpCodes.Mul),
                    new CodeMatch(OpCodes.Newarr),
                    new CodeMatch(OpCodes.Stfld, AccessTools.Field(typeof(GameData), nameof(GameData.factories)))
                ).Advance(1).InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StationStorageOpt), nameof(StationStorageOpt.InitFactoryInfo)))
                );

                return matcher.InstructionEnumeration();
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(MultithreadSystem), nameof(MultithreadSystem.Init))]
            [HarmonyPatch(typeof(MultithreadSystem), nameof(MultithreadSystem.ResetUsedThreadCnt))]
            public static void SetThreadCount(MultithreadSystem __instance)
            {
                numThreads = __instance.usedThreadCnt;
            }
        }
    }
}
