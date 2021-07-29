using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using BepInEx;
using UnityEngine;
using HarmonyLib;
using System.Reflection;

namespace SpherePlanExporter
{
    [BepInPlugin(MOD_GUID, MOD_NAME, MOD_VERSION)]
    [BepInProcess("DSPGAME.exe")]
    public class SpherePlanExporter : BaseUnityPlugin
    {
        public const string MOD_GUID = "com.Selsion.SpherePlanExporter";
        public const string MOD_NAME = "SpherePlanExporter";
        public const string MOD_VERSION = "0.1.0";

        private static StreamWriter stream_writer;
        public const string DIRECTORY_NAME = "sphere_exports";

        internal void Awake()
        {
            var harmony = new Harmony(MOD_GUID);
            harmony.PatchAll(typeof(Patch));
        }

        private static bool CalcCollides(Vector3 pos, DysonSphereLayer layer)
        {
            for (int i = 1; i < layer.nodeCursor; i++)
                if (layer.nodePool[i] != null && layer.nodePool[i].id == i)
                    if ((layer.nodePool[i].pos.normalized - pos).sqrMagnitude < 0.0051122503f)
                        return true;
            return false;
        }

        /*private static DysonNode CalcNearestNode(Vector3 pos, DysonSphereLayer layer)
        {
            float num = 0.0051122503f;
            DysonNode nearestNode = null;
            for (int i = 1; i < layer.nodeCursor; i++)
            {
                if (layer.nodePool[i] != null && layer.nodePool[i].id == i)
                {
                    float sqrMagnitude = (layer.nodePool[i].pos.normalized - pos).sqrMagnitude;
                    if (sqrMagnitude < num)
                    {
                        num = sqrMagnitude;
                        nearestNode = layer.nodePool[i];
                    }
                }
            }
            return nearestNode;
        }*/

        private static string createDirectory(string dir)
        {
            string file_dir = new StringBuilder(GameConfig.gameDocumentFolder).Append(dir + "/").ToString();
            if (!Directory.Exists(file_dir))
            {
                UnityEngine.Debug.Log("creating directory " + file_dir);
                Directory.CreateDirectory(file_dir);
            }
            return file_dir;
        }

        private static string export(string param)
        {
            string[] param_list = param.Split(' ');
            if (param_list.Length == 0 || param_list.Length > 2)
                return "invalid params";

            string filename = "sphere.txt";
            if (param_list.Length == 2)
                filename = param_list[1] + ".txt";

            GameData data = GameMain.data;
            DysonSphere sphere = data.dysonSpheres[data.localStar.index];

            int num = 0;
            if (!int.TryParse(param_list[0], out num) || num < 1 || num > sphere.layerCount)
                return "invalid layer id";

            DysonSphereLayer layer = sphere.layersIdBased[num];

            string file_dir = createDirectory(DIRECTORY_NAME);
            MemoryStream memory_stream = new MemoryStream();
            stream_writer = new StreamWriter((Stream)memory_stream);

            stream_writer.Write(layer.nodeCount + " " + layer.frameCount + "\n");

            for (int i = 0; i < layer.nodeCursor; i++) {
                DysonNode node = layer.nodePool[i];
                if (node == null || node.id != i)
                    continue;
                Vector3 pos = node.pos.normalized;
                stream_writer.Write(pos.x + " " + pos.y + " " + pos.z + "\n");
            }

            for (int i = 0; i < layer.frameCursor; i++) {
                DysonFrame frame = layer.framePool[i];
                if (frame == null || frame.id != i)
                    continue;
                stream_writer.Write(frame.nodeA.id + " " + frame.nodeB.id + "\n");
            }

            // close file stream
            stream_writer.Flush();
            if (memory_stream != null && memory_stream.Length > 0)
            {
                string path = file_dir + filename;
                UnityEngine.Debug.Log("writing to file " + path);
                using (FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    memory_stream.WriteTo(fileStream);
                    fileStream.Flush();
                    fileStream.Close();
                    stream_writer.Close();
                    memory_stream.Close();
                    memory_stream = null;
                }
            }

            return "file written"; // TODO: error checking lol
        }

        private static string frame_coefficient(string param)
        {
            if (param == "")
                return "Invalid Usage. Correct usage: -s-fc layer_id";

            GameData data = GameMain.data;
            DysonSphere sphere = data.dysonSpheres[data.localStar.index];

            int num = 0;
            if (!int.TryParse(param, out num) || num < 1 || num > sphere.layerCount)
                return "invalid layer id";

            DysonSphereLayer layer = sphere.layersIdBased[num];

            double total_angle = 0.0;

            for (int i = 0; i < layer.frameCursor; i++) {
                DysonFrame frame = layer.framePool[i];
                if (frame == null || frame.id != i)
                    continue;
                //stream_writer.Write(frame.nodeA.id + " " + frame.nodeB.id + "\n");
                double angle = Math.Acos(Vector3.Dot(frame.nodeA.pos.normalized, frame.nodeB.pos.normalized));
                total_angle += angle;
            }

            double fc = total_angle / 30.0;

            return fc.ToString();
        }

        private static string import_nodes(string param)
        {
            string[] param_list = param.Split(' ');
            if (param_list.Length != 2)
                return "invalid params";

            GameData data = GameMain.data;
            DysonSphere sphere = data.dysonSpheres[data.localStar.index];

            int num = 0;
            if (!int.TryParse(param_list[0], out num) || num < 1 || num > sphere.layerCount)
                return "invalid layer id";

            DysonSphereLayer layer = sphere.layersIdBased[num];

            string filename = param_list[1];
            string text = System.IO.File.ReadAllText(filename);

            string[] node_positions = text.Split('\n');

            UIDysonPanel panel = UIRoot.instance.uiGame.dysonmap;
            UIDysonBrush_Node brush = panel.brushes[2] as UIDysonBrush_Node;
            // has .nodeProtoId

            int success_count = 0;
            int total_nodes = 0;

            foreach (string node_pos in node_positions) {
                if (node_pos == "")
                    continue;

                string[] coords = node_pos.Split(' ');
                Vector3 pos;
                bool f1 = float.TryParse(coords[0], out pos.x);
                bool f2 =float.TryParse(coords[1], out pos.y);
                bool f3 = float.TryParse(coords[2], out pos.z);

                total_nodes++;
                if (!f1 || !f2 || !f3)
                    continue;

                bool collide = CalcCollides(pos, layer);
                //UIDysonBrush_Node.BuildCondition buildCondition = brush.CheckCondition(pos);

                if(!collide)// && buildCondition == UIDysonBrush_Node.BuildCondition.Ok)
                {
                    layer.NewDysonNode(brush.nodeProtoId, pos * layer.orbitRadius);
                    success_count++;
                }
            }

            return "added " + success_count + " of " + total_nodes + " nodes";
        }
        
        private static string connect_nodes(string param)
        {
            string[] param_list = param.Split(' ');
            if (param_list.Length < 1 || param_list.Length > 2)
                return "invalid params";

            GameData data = GameMain.data;
            DysonSphere sphere = data.dysonSpheres[data.localStar.index];

            int num = 0, id = -1;
            if (!int.TryParse(param_list[0], out num) || num < 1 || num > sphere.layerCount)
                return "invalid layer id";
            if (param_list.Length == 2 && !int.TryParse(param_list[1], out id))
                return "invalid second arugment";

            DysonSphereLayer layer = sphere.layersIdBased[num];
            int n = layer.nodeCount;

            /*var pairs = new List<Tuple<int, int, float>>();
            for (int i = 1; i < layer.nodeCursor; i++) {
                if (layer.nodePool[i] != null && layer.nodePool[i].id == i) {
                    Vector3 pos1 = layer.nodePool[i].pos.normalized;
                    for (int j = i + 1; j < layer.nodeCursor; j++) {
                        if (layer.nodePool[j] != null && layer.nodePool[j].id == j) {
                            Vector3 pos2 = layer.nodePool[j].pos.normalized;
                            pairs.Add(Tuple.Create(i, j, (pos1 - pos2).sqrMagnitude));
                        }
                    }
                 }
            }
            pairs.Sort((x, y) => x.Item3.CompareTo(y.Item3));*/

            UIDysonPanel panel = UIRoot.instance.uiGame.dysonmap;
            UIDysonBrush_Frame brush = panel.brushes[4] as UIDysonBrush_Frame;

            int success_count = 0;
            int total_count = 0;

            var pairs = new List<Tuple<int, float>>[layer.nodeCursor];
            for(int i = 1; i < layer.nodeCursor; i++) {
                if (layer.nodePool[i] == null || layer.nodePool[i].id != i)
                    continue;
                Vector3 pos1 = layer.nodePool[i].pos.normalized;
                pairs[i] = new List<Tuple<int, float>>();
                for (int j = 1; j < layer.nodeCursor; j++) {
                    if (i == j || layer.nodePool[j] == null || layer.nodePool[j].id != j)
                        continue;
                    Vector3 pos2 = layer.nodePool[j].pos.normalized;
                    pairs[i].Add(Tuple.Create(j, (pos1 - pos2).sqrMagnitude));
                }
                pairs[i].Sort((x, y) => x.Item2.CompareTo(y.Item2));
            }

            //return "x";

            for(int idx = 0; idx < 8; idx++) {
                if (id != -1 && idx != id)
                    continue;
                for (int i = 1; i < layer.nodeCursor; i++) {
                    if (layer.nodePool[i] == null || layer.nodePool[i].id != i)
                        continue;
                    DysonNode node1 = layer.nodePool[i];
                    Vector3 pos1 = node1.pos.normalized;
                    DysonNode node2 = layer.nodePool[pairs[i][idx].Item1];
                    Vector3 pos2 = node2.pos;

                    var buildCondition = brush.CheckCondition(pos1, pos2, node1.id, node2.id);
                    if (buildCondition == UIDysonBrush_Frame.BuildCondition.Ok)
                    {
                        layer.NewDysonFrame(0, node1.id, node2.id, brush.isEuler);
                        success_count++;
                    }
                    total_count++;
                }
            }


            /*foreach (var pair in pairs) {
                DysonNode node1 = layer.nodePool[pair.Item1];
                DysonNode node2 = layer.nodePool[pair.Item2];

                Vector3 pos1 = node1.pos.normalized;
                Vector3 pos2 = node2.pos.normalized;

                var buildCondition = brush.CheckCondition(pos1, pos2, node1.id, node2.id);
                if (buildCondition == UIDysonBrush_Frame.BuildCondition.Ok) {
                    layer.NewDysonFrame(0, node1.id, node2.id, brush.isEuler);
                    success_count++;
                }
                total_count++;
            }*/

            return "added " + success_count + " of " + total_count + " frames";
        }
        
        [HarmonyPatch]
        public class Patch
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(XConsole), "RegisterCommands")]
            public static void RegisterCommandsPostfix()
            {
                XConsole.RegisterCommand("s-export", new XConsole.DCommandFunc(export));
                XConsole.RegisterCommand("s-fc", new XConsole.DCommandFunc(frame_coefficient));
                XConsole.RegisterCommand("s-import-nodes", new XConsole.DCommandFunc(import_nodes));
                XConsole.RegisterCommand("s-connect-nodes", new XConsole.DCommandFunc(connect_nodes));
            }
        }
    }
}
