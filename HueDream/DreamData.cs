using HueDream.Hue;
using IniParser;
using IniParser.Model;
using Newtonsoft.Json;
using Q42.HueApi.Models.Groups;
using System;
using System.Collections.Generic;
using System.IO;

namespace HueDream.HueDream {
    [Serializable]
    public static class DreamData {
        
        public static void CheckConfig() {
            string jsonPath = GetConfigPath("huedream.json");
            string iniPath = GetConfigPath("huedream.ini");
            Console.WriteLine("Json path is " + jsonPath + ", ini path is " + iniPath);
            if (!File.Exists(jsonPath) && File.Exists(iniPath)) {
                ConvertConfig(iniPath);
            }
            if (File.Exists(jsonPath)) Console.WriteLine("JSON Path exists.");
            if (File.Exists(iniPath)) Console.WriteLine("INI Path exists.");
        }

        private static void ConvertConfig(string iniPath) {
            Console.WriteLine("Converting config from ini to json.");
            DataObj dObj = new DataObj();
            var parser = new FileIniDataParser();
            IniData data = parser.ReadFile(iniPath);
            dObj.DsIp = data["MAIN"]["DS_IP"];
            dObj.DreamState.type = "SideKick";
            dObj.HueIp = data["MAIN"]["HUE_IP"];            
            dObj.HueAuth = data["MAIN"]["HUE_AUTH"] == "True";
            dObj.HueKey = data["MAIN"]["HUE_KEY"];
            dObj.HueUser = data["MAIN"]["HUE_USER"];
            dObj.DreamState.groupName = data["MAIN"]["DS_GROUP_NAME"] ?? "undefined";
            dObj.DreamState.groupNumber = int.Parse(data["MAIN"]["DS_GROUP_ID"] ?? "100");
            dObj.DreamState.name = data["MAIN"]["DS_DEVICE_NAME"] ?? "HueDream";
            dObj.DreamState.brightness = int.Parse(data["MAIN"]["DS_BRIGHTNESS"] ?? "100");
            dObj.DreamState.saturation = data["MAIN"]["DS_SATURATION"] ?? "FFFFFF";
            dObj.DreamState.color = data["MAIN"]["DS_AMBIENT_COLOR"] ?? "FFFFFF";
            dObj.DreamState.ambientMode = int.Parse(data["MAIN"]["DS_AMBIENT_MODE_TYPE"] ?? "0");
            dObj.DreamState.mode = int.Parse(data["MAIN"]["DS_MODE"] ?? "0");
            dObj.DreamState.scene = int.Parse(data["MAIN"]["DS_AMBIENT_SCENE"] ?? "0");
            dObj.HueLights = JsonConvert.DeserializeObject<List<KeyValuePair<int, string>>>(data["MAIN"]["HUE_LIGHTS"]);
            dObj.HueMap = FixPair(JsonConvert.DeserializeObject<List<KeyValuePair<int, string>>>(data["MAIN"]["HUE_MAP"]));
            SaveJson(dObj);
        }

        private static List<KeyValuePair<int, int>> FixPair(List<KeyValuePair<int, string>> kp) {
            List<KeyValuePair<int, int>> output = new List<KeyValuePair<int, int>>();
            foreach (KeyValuePair<int, string> kpp in kp) {
                output.Add(new KeyValuePair<int, int> ( kpp.Key, int.Parse(kpp.Value)));
            }
            return output;
        }

        /// <summary>
        /// Save our data object 
        /// </summary>
        /// <param name="dObj">A data object</param>
        public static void SaveJson(DataObj dObj) {
            string jsonPath = GetConfigPath("huedream.json");
            JsonSerializer serializer = new JsonSerializer();
            try {
                using (StreamWriter file = File.CreateText(jsonPath)) {
                    serializer.Serialize(file, dObj);
                }
            } catch (IOException e) {
                Console.WriteLine("An IO Exception occurred: " + e.ToString());
            }
        }

        
        /// <summary>
        /// Load Config Data from json file
        /// </summary>
        /// <returns>DataObject</returns>
        public static DataObj LoadJson() {
            DataObj output = null;
            string jsonPath = GetConfigPath("huedream.json");
            if (File.Exists(jsonPath)) {
                try {
                    using (StreamReader file = File.OpenText(jsonPath)) {
                        JsonSerializer js = new JsonSerializer();
                        output = (DataObj)js.Deserialize(file, typeof(DataObj));
                    }
                } catch (IOException e) {
                    Console.WriteLine("An IO Exception occurred: " + e.ToString());
                }
            } else {
                output = new DataObj();
                SaveJson(output);
            }
            return output;
        }

        /// <summary>
        /// Determine if config path is local, or docker
        /// </summary>
        /// <param name="filePath">Config file to check</param>
        /// <returns>Modified path to config file</returns>
        private static string GetConfigPath(string filePath) {
            if (Directory.Exists("/etc/huedream")) {                
                if (File.Exists(filePath)) {
                    Console.WriteLine("We should move our ini to /etc");
                    File.Copy(filePath, "/etc/huedream/" + filePath);
                    if (File.Exists("/etc/huedream/huedream.ini")) {
                        Console.WriteLine("File moved, updating INI path.");
                        File.Delete(filePath);
                    }
                }
                return "/etc/huedream/" + filePath;
            }
            return filePath;
        }

        private static Group[] ListGroups() {
            HueBridge hb = new HueBridge(LoadJson());
            Group[] groups = hb.ListGroups().Result;
            return groups;
        }
    }

}
