using System;
using System.IO;
using HarmonyLib;
using BepInEx;
using UnityEngine;
using BepInEx.Configuration;
using BepInEx.Logging;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Threading;
using System.Text;
using crecheng.DSPModSave;

namespace DSPOptimizations
{
    [BepInPlugin(MOD_GUID, MOD_NAME, MOD_VERSION)]
    //[BepInProcess("DSPGAME.exe")]
    [BepInDependency("crecheng.DSPModSave")]
    public class Plugin : BaseUnityPlugin, IModCanSave
    {
        public const string MOD_GUID = "com.Selsion.DSPOptimizations";
        public const string MOD_NAME = "DSPOptimizations";
        public const string MOD_VERSION = "1.1.17";

        private static Harmony harmony;
        public static ManualLogSource logger;

        internal void Awake()
        {
            logger = Logger;
            //CommandManager.Init();
            CommandManager.QueueInit();
            OptimizationSetManager.Init(this);

            harmony = new Harmony(MOD_GUID);

#if DEBUG
            Directory.CreateDirectory("mmdump"); // or create it manually
            foreach (FileInfo file in new DirectoryInfo("./mmdump").GetFiles())
            {
                file.Delete();
            }
            Environment.SetEnvironmentVariable("MONOMOD_DMD_TYPE", "cecil"); // Also "mb" can work if mono runtime supports it; it can be a bit faster
            Environment.SetEnvironmentVariable("MONOMOD_DMD_DUMP", "mmdump");
#endif
            //harmony.PatchAll(typeof(NoShellDataPatch));
            //LowResShells.Init(this, harmony);
            PatchManager.Init(harmony);
            //LowResShellsLegacySupport.Init(harmony);
#if DEBUG
            Environment.SetEnvironmentVariable("MONOMOD_DMD_DUMP", ""); // Disable to prevent dumping other stuff
#endif

            // TODO: make this compatible with the MoreGraphicsOptions mod
            if (Config.Bind<bool>("General", "disableShadows", false, "Set to true to disable shadows.").Value)
                QualitySettings.shadows = ShadowQuality.Disable;
            /*else
                QualitySettings.shadows = ShadowQuality.All;*/
        }

        public void OnDestroy()
        {
            //LowResShells.OnDestroy();
            OptimizationSetManager.OnDestroy();
            CommandManager.OnDestroy();
            harmony?.UnpatchSelf();
        }

        // TODO: what if LowResShells isn't even enabled? we don't want to discard low res shell data, in case they enable it again
        void IModCanSave.Export(BinaryWriter w)
        {
            /*int version = 1;
            w.Write(version);

            LowResShellsSaveManager.ExportWrapper(w);*/

            int version = 2;
            w.Write(version);
        }

        void IModCanSave.Import(BinaryReader r)
        {
            int version = r.ReadInt32();

            if (version == 1)
            {
                LowResShellsSaveManager.ImportWrapper(r);
            }

            LowResShellsLegacySupport.UpdateShells();
            DysonNodeOpt.InitSPAndCPCounts();
        }

        void IModCanSave.IntoOtherSave()
        {
            // need to guarantee that modded shells can't exist
            // run code for when users delete the file
            // regen existing geo?

            LowResShellsLegacySupport.UpdateShells();
            DysonNodeOpt.InitSPAndCPCounts();

            if (DSPGame.IsMenuDemo)
                return;

            //LowResShellsSaveManager.IntoOtherSave();
        }
    }
}