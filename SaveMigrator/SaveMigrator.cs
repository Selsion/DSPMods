using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
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

        //public static ConfigEntry<bool> updateUnvisitedRockyPlanets;
        public static ConfigEntry<bool> updateGasPlanets;
        public static ConfigEntry<bool> updateDroneTech;
        public static ConfigEntry<bool> updateSailLifeTech;


        internal void Awake()
        {
            var harmony = new Harmony(MOD_GUID);
            harmony.PatchAll(typeof(Patch));
            logger = Logger;

            //updateUnvisitedRockyPlanets = Config.Bind<bool>("General", "updateRockyPlanets", true, "");
            updateGasPlanets = Config.Bind<bool>("General", "updateGasPlanets", true, "Enable to update gas planets to their new types");
            updateDroneTech = Config.Bind<bool>("General", "updateDroneTech", true, "Enable to update the Drone Engine and Communication Control techs");
            updateSailLifeTech = Config.Bind<bool>("General", "updateSailLifeTech", true, "Enable to update the Solar Sail Life techs");
        }

        private static void UpdateSave()
        {
            if (DSPGame.IsMenuDemo)
                return;

            if (updateGasPlanets.Value)
                UpdateGasPlanets();
            if (updateDroneTech.Value)
                UpdateDroneTech();
            if (updateSailLifeTech.Value)
                UpdateSailLifeTech();
        }

        private static void UpdateGasPlanets()
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
                        UpdatePlanet(planet, themeIds);
                        if (planet.theme != old_theme_id)
                            planets_changed++;
                    }
                }
            }

            logger.LogInfo(String.Format("Updated gas giants: {0} giants were changed", planets_changed));
            logger.LogInfo(String.Format("{0} collectors were updated", num_stations_changed));
            logger.LogInfo(String.Format("{0} vessels travelling to collectors were reset", num_vessels_reset));
        }

        private static void UpdatePlanet(PlanetData planet, int[] themeIds)
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
                        UpdateStation(stationComponent, planet);
                        transport.gameData.galacticTransport.RefreshTraffic(stationComponent.gid);
                    }
                }
            }
        }

        private static void UpdateStation(StationComponent station, PlanetData planet)
        {
            ResetIncomingVessels(station);

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

        private static void ResetIncomingVessels(StationComponent station)
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

        private static void UpdateDroneTech()
        {
            // reset relevant values to what they are at the beginning of a game
            ModeConfig freeMode = Configs.freeMode;
            Player mainPlayer = GameMain.mainPlayer;
            Mecha mecha = mainPlayer.mecha;

            MechaDrone[] drones = (MechaDrone[])mecha.drones.Clone();

            int oldDroneCount = mecha.droneCount;
            int oldDroneTaskCount = mecha.droneMovement;
            float oldDroneSpeed = mecha.droneSpeed;

            mecha.droneCount = freeMode.mechaDroneCount;
            mecha.droneSpeed = freeMode.mechaDroneSpeed;
            mecha.droneMovement = freeMode.mechaDroneMovement;

            // check if we need to call the upgrade function for each tech
            for (int i = 2401; i <= 2407; i++)
                CheckTech(i);
            for (int i = 2601; i <= 2606; i++)
                CheckTech(i);

            float droneSpeed = mecha.droneSpeed;
            for (int i = 0; i < mecha.droneCount; i++)
                mecha.drones[i] = drones[i];
            mecha.droneSpeed = droneSpeed;


            logger.LogInfo(String.Format("Updated drone count. Old value: {0} New value: {1}", oldDroneCount, mecha.droneCount));
            logger.LogInfo(String.Format("Updated drone task count. Old value: {0} New value: {1}", oldDroneTaskCount, mecha.droneMovement));
            logger.LogInfo(String.Format("Updated solar drone speed. Old value: {0} New value: {1}", oldDroneSpeed, droneSpeed));
        }

        private static void UpdateSailLifeTech()
        {
            float oldVal = GameMain.history.solarSailLife;
            GameMain.history.solarSailLife = Configs.freeMode.solarSailLife;
            for (int i = 3101; i <= 3106; i++)
                CheckTech(i);
            logger.LogInfo(String.Format("Updated solar sail life. Old value: {0} New value: {1}", oldVal, GameMain.history.solarSailLife));
        }

        public static void CheckTech(int techId)
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
                state.hashUploaded = 0; // is this needed? resets progress if they're already researching it

                GameMain.history.techStates[techId] = state;
            }
        }

        [HarmonyPatch]
        public class Patch
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(GameSave), "LoadCurrentGame")]
            public static void LoadCurrentGamePostfix(string saveName)
            {
                UpdateSave();
            }
        }
    }
}