using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DSPOptimizationsTestMod
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    class CommandAttribute : Attribute
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

    class CommandManager
    {
        private static List<string> cmds;

        public static void Init()
        {
            FindCommands();
        }

        public static void OnDestroy()
        {
            UnregisterCommands();
        }

        private static void AddCommand(string name, Func<string, string> cmd)
        {
            XConsole.RegisterCommand(name, new XConsole.DCommandFunc(cmd));
            cmds.Add(name);
        }

        private static void UnregisterCommands()
        {
            foreach (string name in cmds)
                XConsole.UnregisterCommand(name);
            cmds = null;
        }

        private static bool IsValidCommand(MethodInfo method)
        {
            if (method.ReturnType != typeof(string))
                return false;

            var param = method.GetParameters();
            return param.Length == 1 && !param[0].IsOut && param[0].ParameterType == typeof(string);
        }

        private static void FindCommands()
        {
            cmds = new List<string>();

            var ass = Assembly.GetExecutingAssembly();
            foreach (var type in ass.GetTypes())
            {
                foreach (var method in type.GetMethods())
                {
                    CommandAttribute attr = (CommandAttribute)method.GetCustomAttribute(typeof(CommandAttribute));
                    if (attr != null)
                    {
                        if (IsValidCommand(method))
                            AddCommand(attr.Name, (Func<string, string>)method.CreateDelegate(typeof(Func<string, string>)));
                        else
                            Mod.logger.LogError(string.Format("{0}.{1} is not a valid command", type.Name, method.Name));
                    }
                }
            }

            Mod.logger.LogInfo(string.Format("{0} command{1} loaded", cmds.Count, cmds.Count == 1 ? "" : "s"));
        }
    }
}
