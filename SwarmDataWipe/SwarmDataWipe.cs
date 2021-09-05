using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using UnityEngine;
using HarmonyLib;

namespace SwarmDataWipe
{
    [BepInPlugin(MOD_GUID, MOD_NAME, MOD_VERSION)]
    [BepInProcess("DSPGAME.exe")]
    public class SwarmDataWipe : BaseUnityPlugin
    {
        public const string MOD_GUID = "com.Selsion.SwarmDataWipe";
        public const string MOD_NAME = "SwarmDataWipe";
        public const string MOD_VERSION = "1.0.0";

        internal void Awake()
        {
            var harmony = new Harmony(MOD_GUID);
            harmony.PatchAll(typeof(Patch));
        }

        private enum ESwarmResetError
        {
            None,
            InvalidIndex,
            NoSwarmExists,
            Unknown
        }

        private static ESwarmResetError resetSwarm(int? star_index)
        {
            if (star_index == null)
                return ESwarmResetError.InvalidIndex;

            DysonSphere sphere = GameMain.data.dysonSpheres[star_index ?? -1];
            DysonSwarm swarm = sphere?.swarm;

            if (swarm == null)
                return ESwarmResetError.NoSwarmExists;

            // TODO: reset ejector ui stuff, for the case where the command is run when the ejector ui is open
            // TODO: check if there are any dyson ui references to clear
            
            // clear node references
            for(int i = 0; i < 10; i++)
            {
                DysonSphereLayer layer = sphere.layersSorted[i];
                if (layer == null)
                    continue;
                for (int j = 1; j < layer.nodeCursor; j++)
                {
                    DysonNode node = layer.nodePool[j];
                    if (node != null && node.id == j)
                        node.cpOrdered = 0; // do we need to fix anything else for nodes?
                }
            }

            // note: the ejector objects will automatically retrieve the correct swarm object (through the sphere object),
            // and will reset their orbitId if the swarm orbit changed

            // reset swarm data to how it is when it's first created
            sphere.swarm = new DysonSwarm(sphere);
            sphere.swarm.Init();
            sphere.swarm.ResetNew();

            return ESwarmResetError.None;
        }

        private static string cmdResetLocalSwarm(string param)
        {
            ESwarmResetError err = resetSwarm(GameMain.data.localStar?.index);
            if (err == ESwarmResetError.None)
                return "Successfully reset local swarm";
            else if (err == ESwarmResetError.InvalidIndex)
                return "Failed to reset local swarm: No nearby star";
            else if (err == ESwarmResetError.NoSwarmExists)
                return "Failed to reset local swarm: No swarm exists";
            else // err should be ESwarmResetError.Unknown
                return "Failed to reset local swarm: Cause unknown";
        }

        [HarmonyPatch]
        public class Patch
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(XConsole), "RegisterCommands")]
            public static void RegisterCommandsPostfix()
            {
                XConsole.RegisterCommand("resetLocalSwarm", new XConsole.DCommandFunc(cmdResetLocalSwarm));
            }
        }
    }
}
