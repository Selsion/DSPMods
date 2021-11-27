using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using DSPOptimizations;


namespace DSPOptimizationsTestMod
{
    [BepInPlugin(MOD_GUID, MOD_NAME, MOD_VERSION)]
    [BepInProcess("DSPGAME.exe")]
    [BepInDependency(DSPOptimizations.DSPOptimizations.MOD_GUID)]
    public class Mod : BaseUnityPlugin
    {
        public const string MOD_GUID = "com.Selsion.DSPOptimizationsTestMod";
        public const string MOD_NAME = "DSPOptimizationsTestMod";
        public const string MOD_VERSION = "1.0.0";

        public static ManualLogSource logger;

        internal void Awake()
        {
            logger = Logger;

            TestManager.Init();
            CommandManager.Init();
        }

        internal void OnDestroy()
        {
            TestManager.OnDestroy();
            CommandManager.OnDestroy();
        }
    }
}
