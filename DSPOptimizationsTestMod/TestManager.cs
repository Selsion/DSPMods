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
        public static bool IsInContext(TestContext context)
        {
            if (context == TestContext.Any)
                return true;
            else if (context == TestContext.DysonUI)
                return DSPOptimizations.LowResShellsUI.InitializedUI
                    && UIRoot.instance?.uiGame?.dysonmap?.viewDysonSphere != null;
            else if (context == TestContext.SaveLoaded)
                return DSPGame.Game != null && !DSPGame.IsMenuDemo;
            else if (context == TestContext.ExistsShell)
                return !DSPGame.IsMenuDemo && LowResShellsTest.Shell != null;
            else
                throw new Exception("Unimplemented test context");
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

        private static List<TestContext> GetMatchingContexts(bool contextSatisfied)
        {
            List<TestContext> ret = new List<TestContext>();
            foreach (var context in (TestContext[])Enum.GetValues(typeof(TestContext)))
                if (IsInContext(context) == contextSatisfied)
                    ret.Add(context);
            return ret;
        }

        [Command("runTests")]
        public static string RunAllTests(string param)
        {
            int numTestsPassed = 0, numTestsInContext = 0;
            Dictionary<TestContext, int> skippedCounts = new Dictionary<TestContext, int>();

            foreach (var test in tests)
            {
                if (!IsInContext(test.context))
                {
                    if (!skippedCounts.ContainsKey(test.context))
                        skippedCounts[test.context] = 1;
                    else
                        skippedCounts[test.context] = skippedCounts[test.context] + 1;
                    continue;
                }
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

            string contextMessage = "";
            if(skippedCounts.Count() > 0)
            {
                contextMessage = "\nTests in the following contexts were not run:";
                foreach (var unsatisfiedContext in skippedCounts)
                    contextMessage += string.Format("\n{0}: {1}", Enum.GetName(typeof(TestContext), unsatisfiedContext.Key), unsatisfiedContext.Value);
            }


            return string.Format("Ran {0}/{1} tests. {2}/{0} tests passed.{3}", numTestsInContext, tests.Count, numTestsPassed, contextMessage);
        }
    }
}
