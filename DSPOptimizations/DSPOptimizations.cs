﻿using System;
using System.IO;
using HarmonyLib;
using BepInEx;
using UnityEngine;
using BepInEx.Configuration;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Threading;
using System.Text;
using crecheng.DSPModSave;

namespace DSPOptimizations
{
    [BepInPlugin(MOD_GUID, MOD_NAME, MOD_VERSION)]
    [BepInProcess("DSPGAME.exe")]
    public class DSPOptimizations : BaseUnityPlugin, IModCanSave
    {
        public const string MOD_GUID = "com.Selsion.DSPOptimizations";
        public const string MOD_NAME = "DSPOptimizations";
        public const string MOD_VERSION = "0.1.0";

        public static ConfigEntry<bool> writeOptimizedSave;
        public static ConfigEntry<bool> skipDraws;

        private static Harmony harmony;

        internal void Awake()
        {
            harmony = new Harmony(MOD_GUID);

            //harmony.PatchAll(typeof(NoShellDataPatch));

            LowResShells.Init(this, harmony);

            if (Config.Bind<bool>("General", "disableShadows", false, "Set to true to disable shadows.").Value)
                QualitySettings.shadows = ShadowQuality.Disable;
            else
                QualitySettings.shadows = ShadowQuality.All;
        }

        void IModCanSave.Export(BinaryWriter w)
        {
            int version = 1;
            w.Write(version);

            LowResShellsSaveManager.Export(w);
        }

        void IModCanSave.Import(BinaryReader r)
        {
            int version = r.ReadInt32();

            LowResShellsSaveManager.Import(r);
        }

        void IModCanSave.IntoOtherSave()
        {
            // need to guarantee that modded shells can't exist
            // run code for when users delete the file
            // regen existing geo?

            if (DSPGame.IsMenuDemo)
                return;

            LowResShellsSaveManager.IntoOtherSave();
        }
    }
}