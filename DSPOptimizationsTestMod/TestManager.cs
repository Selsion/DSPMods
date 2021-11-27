using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DSPOptimizationsTestMod.Tests;
using HarmonyLib;

namespace DSPOptimizationsTestMod
{
    enum TestContext
    {
        Any,
        DysonUI,
        SaveLoaded,
        ExistsShell
    }

    struct TestInfo
    {
        public Type type;
        public MethodInfo method;
        public TestContext context;
        public Type patchClass;

        public TestInfo(Type type, MethodInfo method, TestContext context, Type patchClass)
        {
            this.type = type;
            this.method = method;
            this.context = context;
            this.patchClass = patchClass;
        }

        public bool IsInContext()
        {
            if (context == TestContext.Any)
                return true;
            else if (context == TestContext.DysonUI)
                return DSPOptimizations.LowResShellsUI.InitializedUI;
            else if (context == TestContext.SaveLoaded)
                return DSPGame.Game != null && !DSPGame.IsMenuDemo;
            else if (context == TestContext.ExistsShell)
                return LowResShellsTest.Shell != null;
            else
                throw new Exception("Unimplemented test context");
        }
    }
    
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    class TestAttribute : Attribute
    {
        private TestContext context;
        private Type patchClass;

        public TestAttribute(TestContext context, Type patchClass = null)
        {
            this.context = context;
            this.patchClass = patchClass;
        }

        public TestContext Context
        {
            get { return context; }
        }

        public Type PatchClass
        {
            get { return patchClass; }
        }
    }

    class TestManager
    {
        private static List<TestInfo> tests;
        public static Random rng;

        public static void Init()
        {
            FindTests();
            rng = new Random();
        }

        public static void OnDestroy()
        {

        }

        private static bool IsValidTest(MethodInfo method)
        {
            return method.ReturnType == typeof(bool)
                && method.IsStatic
                && method.GetParameters().Length == 0;
        }

        private static void FindTests()
        {
            tests = new List<TestInfo>();

            var ass = Assembly.GetExecutingAssembly();
            foreach (var type in ass.GetTypes())
            {
                foreach (var method in type.GetMethods())
                {
                    TestAttribute attr = (TestAttribute)method.GetCustomAttribute(typeof(TestAttribute));
                    if (attr != null) {
                        if (IsValidTest(method))
                            tests.Add(new TestInfo(type, method, attr.Context, attr.PatchClass));
                        else
                            Mod.logger.LogError(string.Format("{0}.{1} is not a valid test method", type.Name, method.Name));
                    }
                }
            }

            Mod.logger.LogInfo(string.Format("{0} test{1} loaded", tests.Count, tests.Count == 1 ? "" : "s"));
        }

        [Command("runTests")]
        public static string RunAllTests(string param)
        {
            int numTestsPassed = 0, numTestsInContext = 0;

            foreach(var test in tests)
            {
                if (!test.IsInContext())
                    continue;
                numTestsInContext++;

                Harmony harmony = null;

                try
                {
                    if (test.patchClass != null)
                    {
                        harmony = new Harmony(Mod.MOD_GUID);
                        harmony.PatchAll(test.patchClass);
                    }

                    bool ret = (bool)test.method.Invoke(null, new object[] { });
                    if(ret)
                        numTestsPassed++;
                    else
                        Mod.logger.LogError(string.Format("Test {0}.{1} failed", test.type.Name, test.method.Name));
                }
                catch(Exception e)
                {
                    Mod.logger.LogError(string.Format("Test {0}.{1} rasied an exception: {2}", test.type.Name, test.method.Name, e.InnerException));
                }

                if (harmony != null)
                    harmony.UnpatchSelf();
            }


            return string.Format("Ran {0}/{1} tests. {2}/{0} tests passed", numTestsInContext, tests.Count, numTestsPassed);
        }
    }
}
