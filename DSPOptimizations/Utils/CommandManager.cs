using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DSPOptimizations
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class CommandAttribute : Attribute
    {
        private string name;

        public CommandAttribute(string name)
        {
            this.name = name;
        }

        public string Name
        {
            get { return name; }
        }
    }

    [RunPatches(typeof(Patch))]
    public class CommandManager
    {
        private static List<string> cmds;
        private static List<Assembly> srcAssm;

        private static List<Assembly> initQueue;
        private static bool initialized = false;

        public static void QueueInit(Assembly assm = null)
        {
            if (initialized)
            {
                Init(assm);
                return;
            }

            if (initQueue == null)
                initQueue = new List<Assembly>();

            initQueue.Add(assm);
        }

        private static void Init(Assembly assm)
        {
            if (assm == null)
                assm = Assembly.GetExecutingAssembly();
            if(cmds == null)
                cmds = new List<string>();
            if(srcAssm == null)
                srcAssm = new List<Assembly>();
            int total = FindCommands(assm);

            Plugin.logger.LogInfo(string.Format("{0} command{1} loaded from {2}", total, total == 1 ? "" : "s", assm.GetName().Name));
        }

        // only the DSPOptimizations mod should call this without passing in an argument. TODO: change this
        public static void OnDestroy(Assembly assm = null)
        {
            if (cmds == null || cmds.Count == 0)
                return;

            for (int i = 0; i < cmds.Count; i++)
            {
                string name = cmds[i];
                Assembly src = srcAssm[i];
                if(assm == null || assm == src)
                {
                    XConsole.UnregisterCommand(name);
                    cmds.RemoveAt(i);
                    srcAssm.RemoveAt(i);
                    i--;
                }
            }
        }

        private static void AddCommand(string name, Func<string, string> cmd, Assembly assm)
        {
            XConsole.RegisterCommand(name, new XConsole.DCommandFunc(cmd));
            cmds.Add(name);
            srcAssm.Add(assm);
        }

        private static bool IsValidCommand(MethodInfo method)
        {
            if (method.ReturnType != typeof(string))
                return false;

            var param = method.GetParameters();
            return param.Length == 1 && !param[0].IsOut && param[0].ParameterType == typeof(string);
        }

        private static int FindCommands(Assembly assm)
        {
            int total = 0;

            foreach (var type in assm.GetTypes())
            {
                foreach (var method in type.GetMethods())
                {
                    CommandAttribute attr = (CommandAttribute)method.GetCustomAttribute(typeof(CommandAttribute));
                    if (attr != null)
                    {
                        if (IsValidCommand(method)) {
                            AddCommand(attr.Name, (Func<string, string>)method.CreateDelegate(typeof(Func<string, string>)), assm);
                            total++;
                        }
                        else
                            Plugin.logger.LogError(string.Format("{0}.{1} from {2} is not a valid command", type.Name, method.Name, assm.GetName().Name));
                    }
                }
            }

            return total;
        }

        class Patch
        {
            [HarmonyPostfix, HarmonyPatch(typeof(XConsole), nameof(XConsole.InitCommands))]
            public static void InitCommandsPostfix()
            {
                foreach (var assm in initQueue)
                    Init(assm);
                initQueue.Clear();
                initialized = true;
            }
        }
    }
}
