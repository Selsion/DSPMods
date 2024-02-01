using BepInEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DSPOptimizations
{
    class OptimizationSetManager
    {
        // note: submodules must come before the parent modules in this list
        private static List<OptimizationSet> optSetInstances;
        //private static List<OptimizationAttribute> optSetAttrs;

        public static void Init(BaseUnityPlugin plugin)
        {
            optSetInstances = new List<OptimizationSet>();
            //optSetAttrs = new List<OptimizationAttribute>();
            int numSetsLoaded = 0;

            var ass = Assembly.GetExecutingAssembly();
            foreach (var type in ass.GetTypes())
                if (RegisterOptimization(type, false))
                    numSetsLoaded++;

            Plugin.logger.LogInfo(string.Format("{0} optimization set{1} loaded", numSetsLoaded, numSetsLoaded == 1 ? "" : "s"));

            foreach (var set in optSetInstances)
                set.Init(plugin);
        }

        private static bool RegisterOptimization(Type type, bool isSubModule)
        {
            if (!type.IsSubclassOf(typeof(OptimizationSet)))
                return false;

            /*OptimizationAttribute attr = (OptimizationAttribute)type.GetCustomAttribute(typeof(OptimizationAttribute));
            if (attr != null) // will be null for sub-modules
            {
                if (attr.SubModules != null)
                    foreach (var subModule in attr.SubModules)
                        RegisterOptimization(subModule, true);
            }*/

            //if (attr != null || isSubModule)
            {
                var set = (OptimizationSet)Activator.CreateInstance(type);
                optSetInstances.Add(set);
                //optSetAttrs.Add(attr);
                return true;
            }
            /*else
                return false;*/
        }

        public static void OnDestroy()
        {
            foreach (var set in optSetInstances)
                set.OnDestroy();
            optSetInstances.Clear();
            //optSetAttrs.Clear();
        }

        // TODO: when do we destroy the classes? when do we create them? how do we get this to work with ScriptEngine?
    }
}
