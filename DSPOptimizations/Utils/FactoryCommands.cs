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
        [Command("resetLocalFactory")]
        public static string CmdResetLocalFactory(string param)
        {
            var planet = GameMain.localPlanet;
            if (planet == null)
                return "no local planet";

            if (planet.factory == null)
                return "local factory is null";

            planet.audio.nearAudioLogic.RefreshAudiosOnArrayChange();
            planet.physics.nearColliderLogic.RefreshCollidersLive();
            planet.physics.nearColliderLogic.DeleteDeadColliders();

            planet.UnloadFactory();
            ResetFactory(planet);
            PlanetModelingManager.fctPlanetReqList.Enqueue(planet);

            return "successfully reset local factory";
        }

        private static void ResetFactory(PlanetData planet)
        {
            UIRoot.instance.uiGame.ShutAllFunctionWindow();

            var transport = planet.factory.transport;
            var galacticTransport = GameMain.data.galacticTransport;

            for (int i = 1; i < transport.stationCursor; i++)
            {
                var station = transport.stationPool[i];
                if (station != null && station.id == i && station.gid > 0)
                    galacticTransport.RemoveStationComponent(station.gid);
                //Patch.RemoveStationComponentNoRefresh(galacticTransport, station.gid);
            }

            //galacticTransport.RefreshTraffic();

            //planet.factory = null;

            var data = GameMain.data;
            int idx = planet.factory.index;

            PlanetFactory factory = new PlanetFactory();
            factory.Init(data, planet, idx);
            data.factories[idx] = factory;
            planet.factory = factory;

            var warningSystem = data.warningSystem;
            for (int i = 1; i < warningSystem.warningCursor; i++)
            {
                var warning = warningSystem.warningPool[i];
                if (warning.id == i && warning.factoryId == idx)
                    warningSystem.RemoveWarningData(i);
            }

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
        }
    }
}
