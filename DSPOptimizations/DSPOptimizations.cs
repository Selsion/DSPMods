using System;
using System.IO;
using HarmonyLib;
using BepInEx;
using UnityEngine;

namespace DSPOptimizations
{
    [BepInPlugin(MOD_GUID, MOD_NAME, MOD_VERSION)]
    [BepInProcess("DSPGAME.exe")]
    public class DSPOptimizations : BaseUnityPlugin
    {
        public const string MOD_GUID = "com.Selsion.DSPOptimizations";
        public const string MOD_NAME = "DSPOptimizations";
        public const string MOD_VERSION = "0.0.1.0";

        internal void Awake()
        {
            var harmony = new Harmony(MOD_GUID);
            harmony.PatchAll(typeof(Patch));
        }

        [HarmonyPatch]
        public class Patch
        {
            private static long total_bytes_saved = 0; //TODO: add this feature for testing

            private static long bytes_saved(ref DysonShell shell)
            {
                return 0L;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(DysonShell), "Export")]
            public static bool ExportPrefix(ref BinaryWriter w, ref DysonShell __instance)
            {
                // use -1 for the version number. 0 is the first vanilla save version for shells, and >= 1 is for the second version
                w.Write(-1);
                w.Write(__instance.id);
                w.Write(__instance.protoId);
                w.Write(__instance.layerId);
                w.Write(__instance.randSeed);
                w.Write(__instance.polygon.Count);
                for (int i = 0; i < __instance.polygon.Count; i++)
                {
                    w.Write(__instance.polygon[i].x);
                    w.Write(__instance.polygon[i].y);
                    w.Write(__instance.polygon[i].z);
                }
                w.Write(__instance.nodes.Count);
                for (int i = 0; i < __instance.nodes.Count; i++)
                    w.Write(__instance.nodes[i].id);

                int nodecpsLength = __instance.nodecps.Length;
                w.Write(nodecpsLength);
                for (int i = 0; i < nodecpsLength; i++)
                    w.Write(__instance.nodecps[i]);
                int vertcpsLength = __instance.vertcps.Length;
                w.Write(vertcpsLength);
                for (int i = 0; i < vertcpsLength; i+=2)
                {
                    byte mergedData;
                    if (i != vertcpsLength - 1)
                        mergedData = (byte)(__instance.vertcps[i] | __instance.vertcps[i + 1] << 4);
                    else
                        mergedData = (byte)__instance.vertcps[i];
                    w.Write(mergedData);
                }

                //total_bytes_saved += bytes_saved(ref __instance);
                //UnityEngine.Debug.Log(""

                return false;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(DysonShell), "Import")]
            public static bool ImportPrefix(ref BinaryReader r, ref DysonSphere dysonSphere, ref DysonShell __instance)
            {
                if (r.PeekChar() == 65533) // will get this value when the version is -1
                {
                    __instance.SetEmpty();
                    int version = r.ReadInt32();
                    Assert.True(version == -1);

                    __instance.id = r.ReadInt32();
                    __instance.protoId = r.ReadInt32();
                    __instance.layerId = r.ReadInt32();
                    __instance.randSeed = r.ReadInt32();
                    int polygonCount = r.ReadInt32();
                    for (int i = 0; i < polygonCount; i++)
                    {
                        Vector3 item = default(Vector3);
                        item.x = r.ReadSingle();
                        item.y = r.ReadSingle();
                        item.z = r.ReadSingle();
                        __instance.polygon.Add(item);
                    }
                    int nodeCount = r.ReadInt32();
                    for (int index2 = 0; index2 < nodeCount; index2++)
                    {
                        int nodeId = r.ReadInt32();
                        DysonNode node = dysonSphere.FindNode(__instance.layerId, nodeId);
                        Assert.NotNull(node);
                        if (node != null)
                        {
                            __instance.nodeIndexMap[nodeId] = __instance.nodes.Count;
                            __instance.nodes.Add(node);
                            if (!node.shells.Contains(__instance))
                                node.shells.Add(__instance);
                        }
                    }
                    Assert.True(__instance.nodeIndexMap.Count == __instance.nodes.Count);
                    int count = __instance.nodes.Count;
                    for (int i = 0; i < count; i++)
                    {
                        int i2 = (i + 1) % count;
                        DysonFrame dysonFrame = DysonNode.FrameBetween(__instance.nodes[i], __instance.nodes[i2]);
                        Assert.NotNull(dysonFrame);
                        __instance.frames.Add(dysonFrame);
                    }

                    // generate geometry for this shell, since we didn't load it
                    __instance.GenerateGeometry();

                    int nodecpsLength = r.ReadInt32();
                    __instance.nodecps = new int[nodecpsLength];
                    for (int i = 0; i < nodecpsLength; i++)
                        __instance.nodecps[i] = r.ReadInt32();
                    Assert.True(__instance.nodecps.Length == __instance.nodes.Count + 1);
                    int vertcpsLength = r.ReadInt32();
                    __instance.vertcps = new uint[vertcpsLength];
                    for (int i = 0; i < vertcpsLength; i+=2)
                    {
                        byte mergedData = r.ReadByte();
                        __instance.vertcps[i] = (uint)(mergedData & 0xF);
                        if (i != vertcpsLength - 1)
                            __instance.vertcps[i+1] = (uint)(mergedData >> 4);
                    }

                    return false;
                }
                else // vanilla save. run normal import code
                    return true;
            }
        }
    }
}