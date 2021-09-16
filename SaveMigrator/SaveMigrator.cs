using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using UnityEngine;
using HarmonyLib;

namespace SaveMigrator
{
    [BepInPlugin(MOD_GUID, MOD_NAME, MOD_VERSION)]
    [BepInProcess("DSPGAME.exe")]
    public class SaveMigrator : BaseUnityPlugin
    {
        public const string MOD_GUID = "com.Selsion.SaveMigrator";
        public const string MOD_NAME = "SaveMigrator";
        public const string MOD_VERSION = "1.0";
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
                GameData data = GameMain.data;

                // update the theme ID list
                ThemeProtoSet themes = LDB.themes;
                int num_themes = themes.Length;
                int[] themeIds = new int[num_themes];
                for (int i = 0; i < num_themes; i++)
                    themeIds[i] = themes.dataArray[i].ID;
                data.gameDesc.savedThemeIds = themeIds;

                // call update func for all gas planets
                GalaxyData galaxy = data.galaxy;
                for(int i = 0; i < galaxy.starCount; i++)
                {
                    StarData star = galaxy.stars[i];
                    for(int j = 0; j < star.planetCount; j++)
                    {
                        PlanetData planet = star.planets[j];
                        if (planet.type == EPlanetType.Gas)
                            updatePlanet(planet, themeIds);
                    }
                }
            }

            private static void updatePlanet(PlanetData planet, int[] themeIds)
            {
                // get the 5 random numbers made in CreatePlanet needed for SetPlanetTheme
                DotNet35Random rand = new DotNet35Random(planet.infoSeed);
                for(int i = 0; i < 12; i++)
                    rand.NextDouble();
                double rand1 = rand.NextDouble();
                rand.NextDouble();
                double rand2 = rand.NextDouble();
                double rand3 = rand.NextDouble();
                double rand4 = rand.NextDouble();
                int theme_seed = rand.Next();
                PlanetGen.SetPlanetTheme(planet, themeIds, rand1, rand2, rand3, rand4, theme_seed);
            }
        }
    }
}