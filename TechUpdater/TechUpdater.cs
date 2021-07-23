using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using UnityEngine;
using HarmonyLib;

namespace TechUpdater
{
    [BepInPlugin(MOD_GUID, MOD_NAME, MOD_VERSION)]
    [BepInProcess("DSPGAME.exe")]
    public class TechUpdater : BaseUnityPlugin
    {
        public const string MOD_GUID = "com.Selsion.TechUpdater";
        public const string MOD_NAME = "TechUpdater";
        public const string MOD_VERSION = "0.1.0";

        internal void Awake()
        {
            var harmony = new Harmony(MOD_GUID);
            harmony.PatchAll(typeof(Patch));
        }

        [HarmonyPatch]
        public class Patch
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(GameSave), "LoadCurrentGame")]
            public static void LoadCurrentGamePostfix(string saveName)
            {
                // reset relevant values to what they are at the beginning of a game
                ModeConfig freeMode = Configs.freeMode;
                Player mainPlayer = GameMain.mainPlayer;
                Mecha mecha = mainPlayer.mecha;
                mecha.droneCount = freeMode.mechaDroneCount;
                mecha.droneSpeed = freeMode.mechaDroneSpeed;
                mecha.droneMovement = freeMode.mechaDroneMovement;

                // check if we need to call the upgrade function for each tech
                for (int i = 2401; i <= 2407; i++)
                    checkTech(i);
                for (int i = 2601; i <= 2606; i++)
                    checkTech(i);
            }

            public static void checkTech(int techId)
            {
                TechState state = GameMain.history.TechState(techId);
                TechProto proto = LDB.techs.Select(techId);

                // unlock all levels under max level
                for (int i = proto.Level; i < state.curLevel; i++)
                {
                    for (int j = 0; j < proto.UnlockFunctions.Length; j++)
                        GameMain.history.UnlockTechFunction(proto.UnlockFunctions[j], proto.UnlockValues[j], i);
                }

                // unlock last level
                if (state.unlocked)
                    for (int j = 0; j < proto.UnlockFunctions.Length; j++)
                        GameMain.history.UnlockTechFunction(proto.UnlockFunctions[j], proto.UnlockValues[j], state.maxLevel);
                
                // update tech info from proto
                if (state.maxLevel != proto.MaxLevel)
                {
                    // check if the tech has a new max level when we already finished the tech
                    if (state.unlocked)
                    {
                        state.unlocked = false;
                        state.curLevel++;
                    }

                    state.maxLevel = proto.MaxLevel;
                    state.hashNeeded = proto.GetHashNeeded(proto.Level);
                    state.hashUploaded = 0;

                    GameMain.history.techStates[techId] = state;
                }
            }
        }
    }
}