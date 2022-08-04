using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace DSPOptimizations
{
    class FactoryCommands
    {
        private static Dictionary<int, int> planetIdToFactoryIdx;

        //[Command("resetLocalPlanet")]
        public static string CmdResetLocalPlanet(string param)
        {
            var planet = GameMain.localPlanet;
            if (planet == null)
                return "no local planet";

            ResetPlanet(planet);

            return "successfully reset local planet";
        }

        [Command("resetLocalFactory")]
        public static string CmdResetLocalFactory(string param)
        {
            var planet = GameMain.localPlanet;
            if (planet == null)
                return "no local planet";

            if (planet.factory == null)
                return "local factory is null";

            ResetFactory(planet);

            return "successfully reset local factory";
        }

        private static void PrepareReset(PlanetData planet)
        {
            UIRoot.instance.uiGame.ShutAllFunctionWindow();

            var oldFactory = planet.factory;

            var transport = oldFactory.transport;
            var galacticTransport = GameMain.data.galacticTransport;

            for (int i = 1; i < transport.stationCursor; i++)
            {
                var station = transport.stationPool[i];
                if (station != null && station.id == i && station.gid > 0)
                    galacticTransport.RemoveStationComponent(station.gid);
                //Patch.RemoveStationComponentNoRefresh(galacticTransport, station.gid);
            }

            var warningSystem = GameMain.data.warningSystem;
            for (int i = 1; i < warningSystem.warningCursor; i++)
            {
                var warning = warningSystem.warningPool[i];
                if (warning.id == i && warning.factoryId == oldFactory.index)
                    warningSystem.RemoveWarningData(i);
            }

            planet.audio.nearAudioLogic.RefreshAudiosOnArrayChange();
            planet.physics.nearColliderLogic.RefreshCollidersLive();
            planet.physics.nearColliderLogic.DeleteDeadColliders();
        }

        private static void CacheFactoryIndex(PlanetData planet)
        {
            if (planetIdToFactoryIdx == null)
                planetIdToFactoryIdx = new Dictionary<int, int>();
            if (planetIdToFactoryIdx.ContainsKey(planet.id))
                planetIdToFactoryIdx[planet.id] = planet.factory.index;
            else
                planetIdToFactoryIdx.Add(planet.id, planet.factory.index);
        }
        
        private static void ResetPlanet(PlanetData planet)
        {
            PrepareReset(planet);

            var data = GameMain.data;
            data.LeavePlanet();
            planet.Unload();

            CacheFactoryIndex(planet);

            planet.loaded = planet.loading = planet.factoryLoaded = planet.factoryLoading = false;
            planet.data = null;
            planet.factory = null;
            planet.onLoaded += ResetPlanetFinish;
            PlanetModelingManager.RequestLoadPlanet(planet);   
        }

        private static void ResetPlanetFinish(PlanetData planet)
        {
            if (!planetIdToFactoryIdx.TryGetValue(planet.id, out int idx))
                return;

            planetIdToFactoryIdx.Remove(planet.id);
            planet.onLoaded -= ResetPlanetFinish;

            // is this needed?
            /*if(planet.galaxy.birthPlanetId == planet.id)
                planet.GenBirthPoints();*/

            var data = GameMain.data;

            PlanetFactory factory = new PlanetFactory();
            factory.Init(data, planet, idx);
            data.factories[idx] = factory;
            planet.factory = factory;

            //data.OnActivePlanetLoaded(planet);
            //PlanetModelingManager.RequestLoadPlanetFactory(planet);
        }

        private static void ResetFactory(PlanetData planet)
        {
            PrepareReset(planet);
            planet.UnloadFactory();

            var oldFactory = planet.factory;

            var data = GameMain.data;
            int idx = oldFactory.index;

            // this is needed to preserve vein and vege info
            var planetData = planet.data;
            planetData.veinCursor = oldFactory.veinCursor;
            planetData.SetVeinCapacity(planetData.veinCursor + 2);
            Array.Copy(oldFactory.veinPool, planetData.veinPool, planetData.veinCursor);
            planetData.vegeCursor = oldFactory.vegeCursor;
            planetData.SetVegeCapacity(planetData.vegeCursor + 2);
            Array.Copy(oldFactory.vegePool, planetData.vegePool, planetData.vegeCursor);

            // needed to preserve new vein info
            lock (planet.veinGroupsLock) {
                int veinGroupsLength = Math.Max(oldFactory.veinGroups.Length, 1);
                planet.veinGroups = new VeinGroup[veinGroupsLength];
                Array.Copy(oldFactory.veinGroups, planet.veinGroups, veinGroupsLength);
            }

            var oldPlatformSystem = oldFactory.platformSystem;

            PlanetFactory factory = new PlanetFactory();
            factory.Init(data, planet, idx);
            data.factories[idx] = factory;
            planet.factory = factory;

            factory.platformSystem = oldPlatformSystem;
            oldPlatformSystem.factory = factory;

            // this code is disabled since i can't test it, and since this achievement fix doesn't even matter
            /*foreach (KeyValuePair<int, AchievementDeterminator> kvp in GameMain.gameScenario.achievementLogic.determinators)
            {
                if (kvp.Value is ACH_BroadcastStar)
                {
                    var ach = kvp.Value as ACH_BroadcastStar;
                    if(ach.artStars is null)
                        break;

                    // there are 4 art star arrays for the 4 types to check for the achievement
                    for (int i = 0; i < 4; i++) {
                        int overwriteIdx = 0;
                        for (int j = 0; j < ach.eCursor[i]; j++) {
                            ach.artStars[i][overwriteIdx] = ach.artStars[i][j];
                            if (ach.artStars[i][j].factoryId != idx)
                                overwriteIdx++;
                        }
                        int oldCursor = ach.eCursor[i];
                        ach.eCursor[i] = overwriteIdx;
                        for (; overwriteIdx < oldCursor; overwriteIdx++)
                            ach.artStars[i][i].SetNull();
                    }

                    break;
                }
            }*/

            PlanetModelingManager.fctPlanetReqList.Enqueue(planet);
        }
    }
}
