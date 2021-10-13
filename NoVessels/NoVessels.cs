using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using BepInEx;

namespace NoVessels
{
    [BepInPlugin(MOD_GUID, MOD_NAME, MOD_VERSION)]
    [BepInProcess("DSPGAME.exe")]
    public class NoVessels : BaseUnityPlugin
    {
        public const string MOD_GUID = "com.Selsion.NoVessels";
        public const string MOD_NAME = "NoVessels";
        public const string MOD_VERSION = "1.0.0";

        internal void Awake()
        {
            var harmony = new Harmony(MOD_GUID);
            harmony.PatchAll(typeof(Patch));
        }

        [HarmonyPatch]
        public class Patch
        {
            [HarmonyPrefix]
            [HarmonyPatch(typeof(LogisticShipRenderer), "Draw")]
            [HarmonyPatch(typeof(LogisticShipRenderer), "Update")]
            [HarmonyPatch(typeof(LogisticShipUIRenderer), "Draw")]
            [HarmonyPatch(typeof(LogisticShipUIRenderer), "Update")]
            public static bool Prefix()
            {
                return false;
            }
        }
    }
}
