using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;

namespace DSPOptimizations
{
    class PatchManager
    {
        public static void Init(Harmony harmony)
        {
            var assm = Assembly.GetExecutingAssembly();
            foreach (var type in assm.GetTypes())
                //if (type.IsSubclassOf(typeof(OptimizationSet)))
                    foreach(var attr in type.GetCustomAttributes())
                        if(attr is RunPatchesAttribute)
                            harmony.PatchAll((attr as RunPatchesAttribute).Patches);
        }
    }
}
