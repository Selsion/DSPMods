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
    [Optimization("LayerDismantleOpt", "", false, new Type[] { })]
    public class LayerDismantleOpt
    {
        public static bool dismantling = false;

        class Patch
        {
            [HarmonyTranspiler]
            [HarmonyPatch(typeof(DysonSphereLayer), "RemoveDysonFrame")]
            [HarmonyPatch(typeof(DysonSphereLayer), "RemoveDysonNode")]
            static IEnumerable<CodeInstruction> RemoveRebuildModelsCall(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher matcher = new CodeMatcher(instructions, generator);

                matcher.MatchForward(true,
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(DysonSphereLayer), nameof(DysonSphereLayer.dysonSphere))),
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(DysonSphere), nameof(DysonSphere.modelRenderer))),
                    new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(DysonSphereSegmentRenderer), nameof(DysonSphereSegmentRenderer.RebuildModels)))
                ).Advance(1).CreateLabel(out Label afterCall).Advance(-4).InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(LayerDismantleOpt), nameof(LayerDismantleOpt.dismantling))),
                    new CodeInstruction(OpCodes.Brtrue, afterCall)
                );

                return matcher.InstructionEnumeration();
            }

            [HarmonyPrefix, HarmonyPatch(typeof(DysonSphereLayer), "RemoveAllStructure")]
            public static void DismantlePrefix(DysonSphereLayer __instance)
            {
                dismantling = true;

                //var swarm = __instance.dysonSphere.swarm;
                //swarm.tmpSolarSail = new DysonSail[swarm.sailCapacity];
            }

            [HarmonyPostfix, HarmonyPatch(typeof(DysonSphereLayer), "RemoveAllStructure")]
            public static void DismantlePostfix(DysonSphereLayer __instance)
            {
                __instance.dysonSphere.modelRenderer.RebuildModels();
                dismantling = false;

                /*var swarm = __instance.dysonSphere.swarm;
                swarm.swarmBuffer.SetData(swarm.tmpSolarSail);
                swarm.swarmInfoBuffer.SetData(swarm.sailInfos);
                swarm.tmpSolarSail = new DysonSail[1];*/
            }

            /*[HarmonyPostfix, HarmonyPatch(typeof(DysonSwarm), "SetSailCapacity")]
            public static void SetCapacityPostfix(DysonSwarm __instance, int newCap)
            {
                if (dismantling)
                {
                    var old = __instance.tmpSolarSail;
                    __instance.tmpSolarSail = new DysonSail[newCap];
                    if (old != null)
                        Array.Copy(old, __instance.tmpSolarSail, old.Length);
                }
            }*/
        }
    }
}
