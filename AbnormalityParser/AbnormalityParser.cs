using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using BepInEx;
using System.IO;

namespace AbnormalityParser
{
    [BepInPlugin(MOD_GUID, MOD_NAME, MOD_VERSION)]
    [BepInProcess("DSPGAME.exe")]
    public class AbnormalityParser : BaseUnityPlugin
    {
        public const string MOD_GUID = "com.Selsion.AbnormalityParser";
        public const string MOD_NAME = "AbnormalityParser";
        public const string MOD_VERSION = "1.2.0";

        public const string DIRECTORY_NAME = "AbnormalityParserMod";
        public const string DEFAULT_FILENAME = "abnormality_info.txt";

        internal void Awake()
        {
            var harmony = new Harmony(MOD_GUID);
            harmony.PatchAll(typeof(Patch));
        }

        private static string CheckAbnormalities(string param)
        {
            GameAbnormalityData data = GameMain.data.gameAbnormality;
            int MAX_DATA_COUNT = GameAbnormalityData.MAX_DATA_COUNT;
            
            int num_detected = 0;
            string ret = "";

            for (int i = MAX_DATA_COUNT; i < data.runtimeDatas.Length; i += MAX_DATA_COUNT)
            {
                int protoId = i / MAX_DATA_COUNT;
                if (data.runtimeDatas[i].protoId == protoId)
                {
                    num_detected++;
                    ret = ret + LDB.abnormalities.Select(protoId).DeterminatorName + "\n";
                }
            }

            return num_detected + " abnormality type" + (num_detected == 1 ? "" : "s") + " detected" + (num_detected > 0 ? ":\n" : ".") + ret;
        }

        private static string WriteStringsToFile(List<string> output, string filename)
        {
            // create file directory if needed
            string file_dir = new StringBuilder(GameConfig.gameDocumentFolder).Append(DIRECTORY_NAME + "/").ToString();
            if (!Directory.Exists(file_dir))
            {
                UnityEngine.Debug.Log("creating directory " + file_dir);
                Directory.CreateDirectory(file_dir);
            }

            // open file stream
            MemoryStream memory_stream = new MemoryStream();
            StreamWriter w = new StreamWriter((Stream)memory_stream);

            // write data
            output.ForEach(s =>
            {
                w.Write(s);
            });

            // close file stream
            w.Flush();
            if (memory_stream != null && memory_stream.Length > 0)
            {
                string path = file_dir + filename;
                UnityEngine.Debug.Log("writing to file " + path);
                using (FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    memory_stream.WriteTo(fileStream);
                    fileStream.Flush();
                    fileStream.Close();
                    w.Close();
                    memory_stream.Close();
                    memory_stream = null;
                }
                return path;
            }
            else
                throw new Exception("Unable to write file");
        }

        private static string DumpAbnormalityInfo(string param)
        {
            string[] param_list = param.Split(' ');
            if (param_list.Length > 1)
                return "invalid params";

            AbnormalityRuntimeData[] runtimeDatas = GameMain.data.gameAbnormality.runtimeDatas;
            int MAX_DATA_COUNT = GameAbnormalityData.MAX_DATA_COUNT;

            List<string> output = new List<string>();

            for(int i = MAX_DATA_COUNT; i < runtimeDatas.Length; i++)
            {
                AbnormalityRuntimeData data = runtimeDatas[i];
                if(data.protoId == i / MAX_DATA_COUNT)
                {
                    string name = LDB.abnormalities.Select(data.protoId).DeterminatorName;
                    output.Add("Found abnormality " + name + " at index " + i + ". Message:\n" + data.ToMessageString() + "\n\n");
                }
            }

            if (output.Count == 0)
                return "No abnormality types were detected. No data written to disk.";
            else
            {
                string filename = param == "" ? DEFAULT_FILENAME : param;
                string filepath = WriteStringsToFile(output, filename);
                return "Dumped abnormality data in " + filepath;
            }
        }

        private static string ClearAbnormalities(string param)
        {
            AbnormalityRuntimeData[] runtimeData = GameMain.data.gameAbnormality.runtimeDatas;
            Array.Clear(runtimeData, 0, runtimeData.Length);
            return "Cleared abnormality data";
        }

        [HarmonyPatch]
        public class Patch
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(XConsole), "RegisterCommands")]
            public static void RegisterCommandsPostfix()
            {
                XConsole.RegisterCommand("checkAbnormalities", new XConsole.DCommandFunc(CheckAbnormalities));
                XConsole.RegisterCommand("dumpAbnormalityInfo", new XConsole.DCommandFunc(DumpAbnormalityInfo));
                XConsole.RegisterCommand("clearAbnormalities", new XConsole.DCommandFunc(ClearAbnormalities));
            }
        }
    }
}
