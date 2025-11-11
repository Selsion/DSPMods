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
            {
                try
                {
                    //if (type.IsSubclassOf(typeof(OptimizationSet)))
                    foreach (var attr in type.GetCustomAttributes())
                        if (attr is RunPatchesAttribute)
                            //if ((attr as RunPatchesAttribute).IsEnabled())
                                harmony.PatchAll((attr as RunPatchesAttribute).Patches);
                }
                catch(Exception e)
                {
                    Plugin.logger.LogError("PatchManager error: " + type.Name + "\n" + e.StackTrace);
                }
            }
        }
    }
}
