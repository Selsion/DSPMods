using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSPOptimizations
{
    //[RunPatches(typeof(Patch))]
    [Optimization("PrebuildOpt", "Reduces permanent lag caused by placing large blueprints", false, new Type[] { })]
    class PrebuildOpt : OptimizationSet
    {

        class Patch
        {
            // TODO: fix all the references to the IDs
            [HarmonyPrefix]
            [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.Export))]
            public static void PrebuildOpt_Import_Postfix(PlanetFactory __instance)
            {
                return;

                int curShift = 0;
                for (int i = 1; i < __instance.prebuildCursor; i++)
                {
                    if (__instance.prebuildPool[i].id == i) {
                        __instance.prebuildPool[i - curShift] = __instance.prebuildPool[i];
                        Array.Copy(__instance.prebuildConnPool, i * 16, __instance.prebuildConnPool, (i - curShift) * 16, 16);
                    } else
                        curShift++;
                }
                __instance.prebuildCursor -= curShift;
                __instance.prebuildRecycleCursor = 0;

                int newCapacity = 256;
                while (__instance.prebuildCursor > newCapacity)
                    newCapacity *= 2;
                __instance.SetPrebuildCapacity(newCapacity);
            }
        }
    }
}