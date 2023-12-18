using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSPOptimizations
{
    class SphereCommands
    {
        private enum EResetError
        {
            None,
            InvalidIndex,
            DoesNotExist,
            Unknown
        }

        private static EResetError ResetSwarm(int star_index)
        {
            DysonSphere sphere = GameMain.data.dysonSpheres[star_index];
            DysonSwarm swarm = sphere?.swarm;

            if (swarm == null)
                return EResetError.DoesNotExist;

            // TODO: reset ejector ui stuff, for the case where the command is run when the ejector ui is open
            // TODO: check if there are any dyson ui references to clear

            // clear node references
            for (int i = 0; i < 10; i++)
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

            return EResetError.None;
        }

        [Command("resetLocalSwarm")]
        public static string CmdResetLocalSwarm(string param)
        {
            var localStar = GameMain.data.localStar;
            if (localStar == null)
                return "Failed to reset local swarm: No nearby star";

            EResetError err = ResetSwarm(localStar.index);
            if (err == EResetError.None)
                return "Successfully reset local swarm";
            else if (err == EResetError.DoesNotExist)
                return "Failed to reset local swarm: No swarm exists";
            else // err should be EResetError.Unknown
                return "Failed to reset local swarm: Cause unknown";
        }

        [Command("resetLocalSphereLayer")]
        public static string CmdResetLocalSphereLayer(string param)
        {
            if (param == "")
                return "Invalid Usage. Correct usage: -resetLocalSphereLayer layerId";

            GameData data = GameMain.data;
            var localStar = data.localStar;
            if (localStar == null)
                return "Failed to reset local sphere layer: No nearby star";

            DysonSphere sphere = data.dysonSpheres[localStar.index];
            if (sphere == null)
                return "Failed to reset local sphere layer: sphere does not exist";

            if (!int.TryParse(param, out int id) || id < 1 || id > 10)
                return "Failed to reset local sphere layer: invalid layer id";

            var layer = sphere.layersIdBased[id];
            if (layer == null)
                return "Failed to reset local sphere layer: layer does not exist";

            for (int i = 0; i < layer.nodePool.Length; i++)
            {
                DysonNode dysonNode = layer.nodePool[i];
                if (dysonNode != null)
                {
                    sphere.swarm.OnNodeRemove(layer.id, i);
                    sphere.RemoveAutoNode(layer.nodePool[i]);
                    sphere.RemoveNodeRocket(layer.nodePool[i]);
                    sphere.RemoveDysonNodeRData(layer.nodePool[i]);
                }
            }

            sphere.RemoveLayer(id);

            var editor = UIRoot._instance.uiGame.dysonEditor;
            if (!editor.IsRender(id, false, true))
                editor.SwitchRenderState(id, false, true);
            if (!editor.IsRender(id, false, false))
                editor.SwitchRenderState(id, false, false);
            editor.selection.ClearAllSelection();

            sphere.CheckAutoNodes();
            sphere.PickAutoNode();
            sphere.modelRenderer.RebuildModels();

            return "Successfully reset sphere layer " + id;
        }

        [Command("resetAllSpheresAndSwarms")]
        public static string CmdResetAllSpheresAndSwarms(string param)
        {
            int totalReset = 0;

            var data = GameMain.data;
            for(int i = 0; i < data.dysonSpheres.Length; i++)
            {
                if(data.dysonSpheres[i] != null)
                {
                    data.dysonSpheres[i] = new DysonSphere();
                    data.dysonSpheres[i].Init(data, data.galaxy.stars[i]);
                    data.dysonSpheres[i].ResetNew();

                    totalReset++;
                }
            }

            return string.Format("Reset spheres and swarms around {0}/{1} stars", totalReset, data.dysonSpheres.Length);
        }
    }
}
