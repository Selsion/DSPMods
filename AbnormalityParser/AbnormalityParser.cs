using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using BepInEx;

namespace AbnormalityParser
{
    [BepInPlugin(MOD_GUID, MOD_NAME, MOD_VERSION)]
    [BepInProcess("DSPGAME.exe")]
    public class AbnormalityParser : BaseUnityPlugin
    {
        public const string MOD_GUID = "com.Selsion.AbnormalityParser";
        public const string MOD_NAME = "AbnormalityParser";
        public const string MOD_VERSION = "1.0.0";

        private static string[] bit_names = { "PROTO", "TECH", "RECIPE", "STORAGE", "CONSOLE", "MECHA", "DYSON SPHERE", "PRODUCTION", "GALAXY", "FACTORY", "NUM" };

        internal void Awake()
        {
            var harmony = new Harmony(MOD_GUID);
            harmony.PatchAll(typeof(Patch));
        }

        private static bool IsBitSet(int bit)
        {
            return (GameMain.abnormalityCheck.checkMask & 1 << bit) != 0;
        }

        private static string CheckMask(string param)
        {
            int count = 0;
            string ret = "";
            for (int i = 0; i < bit_names.Length; i++)
            {
                if (IsBitSet(i))
                {
                    ret = ret + "\n" + bit_names[i];
                    count++;
                }
            }
            return count + " abnormality type" + (count != 1 ? "s" : "") + " detected" + (count > 0 ? ":" : "") + ret;
        }

        [HarmonyPatch]
        public class Patch
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(XConsole), "RegisterCommands")]
            public static void RegisterCommandsPostfix()
            {
                XConsole.RegisterCommand("checkMask", new XConsole.DCommandFunc(CheckMask));
            }
        }
    }
}
