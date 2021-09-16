using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
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
        public const string MOD_VERSION = "1.0.1";

        static ManualLogSource logger;
        static int num_stations_changed;
        static int num_vessels_reset;

        internal void Awake()
        {
            var harmony = new Harmony(MOD_GUID);
            harmony.PatchAll(typeof(Patch));
            logger = Logger;
        }

        private static void updateSave()
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
            int planets_changed = num_stations_changed = num_vessels_reset = 0;
            GalaxyData galaxy = data.galaxy;
            for (int i = 0; i < galaxy.starCount; i++)
            {
                StarData star = galaxy.stars[i];
                for (int j = 0; j < star.planetCount; j++)
                {
                    PlanetData planet = star.planets[j];
                    if (planet.type == EPlanetType.Gas) {
                        int old_theme_id = planet.theme;
                        updatePlanet(planet, themeIds);
                        if (planet.theme != old_theme_id)
                            planets_changed++;
                    }
                }
            }

            logger.LogInfo(String.Format("Updated gas giants: {0} giants were changed", planets_changed));
            logger.LogInfo(String.Format("{0} collectors were updated", num_stations_changed));
            logger.LogInfo(String.Format("{0} vessels travelling to collectors were reset", num_vessels_reset));
        }

        private static void updatePlanet(PlanetData planet, int[] themeIds)
        {
            // get the 5 random numbers made in CreatePlanet needed for SetPlanetTheme
            DotNet35Random rand = new DotNet35Random(planet.infoSeed);
            for (int i = 0; i < 12; i++)
                rand.NextDouble();
            double rand1 = rand.NextDouble();
            rand.NextDouble();
            double rand2 = rand.NextDouble();
            double rand3 = rand.NextDouble();
            double rand4 = rand.NextDouble();
            int theme_seed = rand.Next();
            PlanetGen.SetPlanetTheme(planet, themeIds, rand1, rand2, rand3, rand4, theme_seed);

            // update any existing collectors to the new items
            PlanetTransport transport = planet.factory?.transport;
            if (transport != null)
            {
                for (int i = 1; i < transport.stationCursor; i++)
                {
                    StationComponent stationComponent = transport.stationPool[i];
                    if (stationComponent != null && stationComponent.id == i)
                    {
                        updateStation(stationComponent, planet);
                        transport.gameData.galacticTransport.RefreshTraffic(stationComponent.gid);
                    }
                }
            }
        }

        private static void updateStation(StationComponent station, PlanetData planet)
        {
            resetIncomingVessels(station);

            EntityData entity = planet.factory.entityPool[station.entityId];
            ModelProto modelProto = LDB.models.Select((int)entity.modelIndex);
            PrefabDesc prefabDesc = modelProto.prefabDesc;

            int numGasItems = planet.gasItems.Length;
            station.collectionIds = new int[numGasItems];
            station.collectionPerTick = new float[numGasItems];
            for (int i = 0; i < numGasItems; i++)
            {
                station.collectionIds[i] = planet.gasItems[i];

                double m = 0.0;
                if ((double)prefabDesc.stationCollectSpeed * planet.gasTotalHeat != 0.0)
                    m = 1.0 - (double)prefabDesc.workEnergyPerTick / ((double)prefabDesc.stationCollectSpeed * planet.gasTotalHeat * 0.016666666666666666);

                if (m == 0.0)
                    station.collectionPerTick[i] = planet.gasSpeeds[i] * 0.016666668f * (float)prefabDesc.stationCollectSpeed;
                else
                    station.collectionPerTick[i] = planet.gasSpeeds[i] * 0.016666668f * (float)prefabDesc.stationCollectSpeed * (float)m;
            }


            station.storage = new StationStore[prefabDesc.stationMaxItemKinds];
            station.collectSpeed = prefabDesc.stationCollectSpeed;
            station.currentCollections = new float[numGasItems];
            for (int j = 0; j < numGasItems && j <= prefabDesc.stationMaxItemKinds - 1; j++)
            {
                station.storage[j].itemId = station.collectionIds[j];
                station.storage[j].count = 0;
                station.storage[j].remoteLogic = ELogisticStorage.Supply;
                station.storage[j].max = prefabDesc.stationMaxItemCount;
                station.currentCollections[j] = 0f;
            }

            num_stations_changed++;
        }

        private static void resetIncomingVessels(StationComponent station)
        {
            StationComponent[] gStationPool = GameMain.data.galacticTransport.stationPool;
            for (int j = 0; j < station.remotePairCount; j++)
            {
                SupplyDemandPair pair = station.remotePairs[j]; // assume pair.supplyId == station.gid
                StationComponent otherStation = gStationPool[pair.demandId];
                if (otherStation != null)
                {
                    for (int i = 0; i < otherStation.workShipCount; i++)
                    {
                        ShipData workShipData = otherStation.workShipDatas[i];
                        RemoteLogisticOrder workShipOrder = otherStation.workShipOrders[i];

                        if (workShipOrder.otherStationGId == station.gid && workShipData.direction > 0)
                        {
                            workShipData.t = 0.0f;
                            if (otherStation.workShipOrders[i].itemId > 0)
                            {
                                lock (otherStation.storage)
                                {
                                    if (otherStation.storage[workShipOrder.thisIndex].itemId == workShipOrder.itemId)
                                        otherStation.storage[workShipOrder.thisIndex].remoteOrder -= workShipOrder.thisOrdered;
                                }
                                workShipOrder.ClearThis();
                            }
                            Array.Copy((Array)otherStation.workShipDatas, i + 1, (Array)otherStation.workShipDatas, i, otherStation.workShipDatas.Length - i - 1);
                            Array.Copy((Array)otherStation.workShipOrders, i + 1, (Array)otherStation.workShipOrders, i, otherStation.workShipOrders.Length - i - 1);
                            --otherStation.workShipCount;
                            ++otherStation.idleShipCount;
                            otherStation.WorkShipBackToIdle(workShipData.shipIndex);
                            Array.Clear((Array)otherStation.workShipDatas, otherStation.workShipCount, otherStation.workShipDatas.Length - otherStation.workShipCount);
                            Array.Clear((Array)otherStation.workShipOrders, otherStation.workShipCount, otherStation.workShipOrders.Length - otherStation.workShipCount);
                            --i;
                            num_vessels_reset++;
                            continue;
                        }
                    }
                }
            }
        }

        [HarmonyPatch]
        public class Patch
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(GameSave), "LoadCurrentGame")]
            public static void LoadCurrentGamePostfix(string saveName)
            {
                updateSave();
            }
        }
    }
}