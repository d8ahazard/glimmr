using HueDream.HueControl;
using IniParser;
using IniParser.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace HueDream.HueDream {
    [Serializable]
    public class DreamData {
        public string iniPath = "huedream.ini";
        public string DS_IP { get; set; }
        public string DS_GROUP_NAME { get; set; }
        public string DS_DEVICE_NAME { get; set; }
        public int DS_GROUP_ID { get; set; }
        public int DS_BRIGHTNESS { get; set; }
        public string DS_SATURATION { get; set; }
        public string DS_AMBIENT_COLOR { get; set; }
        public int DS_AMBIENT_MODE_TYPE { get; set; }
        public int DS_AMBIENT_SCENE { get; set; }
        public int DS_MODE { get; set; }
        public string HUE_IP { get; set; }
        public bool HUE_SYNC { get; set; }
        public bool HUE_AUTH { get; set; }
        public string HUE_KEY { get; set; }
        public string HUE_USER { get; set; }
        public List<KeyValuePair<int, string>> HUE_LIGHTS { get; set; }
        public List<KeyValuePair<int, string>> HUE_MAP { get; set; }

        public bool SyncEnabled() {
            loadData();
            return HUE_SYNC;
        }

        public DreamData() {
            if (Directory.Exists("/etc/huedream")) {
                Console.WriteLine("Directory exists.");
                if (File.Exists(iniPath)) {
                    Console.WriteLine("We should move our ini to /etc");
                    File.Copy(iniPath, "/etc/huedream/huedream.ini");
                    if (File.Exists("/etc/huedream/huedream.ini")) {
                        Console.WriteLine("File moved, updating INI path.");
                        File.Delete(iniPath);
                    }
                }
                if (File.Exists("/etc/huedream/huedream.ini")) {
                    iniPath = "/etc/huedream/huedream.ini";
                }
            }


            if (!File.Exists(iniPath)) {
                DS_IP = "0.0.0.0";
                DS_GROUP_ID = 100;
                DS_GROUP_NAME = "undefined";
                DS_DEVICE_NAME = "HueDream";
                DS_BRIGHTNESS = 100;
                DS_SATURATION = "FFFFFF";
                DS_AMBIENT_COLOR = "FFFFFF";
                DS_AMBIENT_MODE_TYPE = 0;
                DS_AMBIENT_SCENE = 0;
                DS_MODE = 0;
                HUE_IP = HueBridge.findBridge();
                HUE_SYNC = false;
                HUE_AUTH = false;
                HUE_KEY = "";
                HUE_USER = "";
                HUE_LIGHTS = new List<KeyValuePair<int, string>>();
                HUE_MAP = new List<KeyValuePair<int, string>>();
                var parser = new FileIniDataParser();
                IniData data = new IniData();
                data["MAIN"]["DS_IP"] = DS_IP;
                data["MAIN"]["HUE_IP"] = HUE_IP;
                data["MAIN"]["HUE_SYNC"] = HUE_SYNC ? "True" : "False";
                data["MAIN"]["HUE_AUTH"] = HUE_AUTH ? "True" : "False";
                data["MAIN"]["HUE_KEY"] = HUE_KEY;
                data["MAIN"]["HUE_USER"] = HUE_USER;
                data["MAIN"]["DS_GROUP_NAME"] = DS_GROUP_NAME;
                data["MAIN"]["DS_GROUP_ID"] = DS_GROUP_ID.ToString();
                data["MAIN"]["DS_DEVICE_NAME"] = DS_DEVICE_NAME;
                data["MAIN"]["DS_BRIGHTNESS"] = DS_BRIGHTNESS.ToString();
                data["MAIN"]["DS_SATURATION"] = DS_SATURATION;
                data["MAIN"]["DS_AMBIENT_COLOR"] = DS_AMBIENT_COLOR;
                data["MAIN"]["DS_AMBIENT_MODE_TYPE"] = DS_AMBIENT_MODE_TYPE.ToString();
                data["MAIN"]["DS_AMBIENT_SCENE"] = DS_AMBIENT_SCENE.ToString();
                data["MAIN"]["DS_MODE"] = DS_MODE.ToString();
                data["MAIN"]["HUE_LIGHTS"] = JsonConvert.SerializeObject(HUE_LIGHTS);
                data["MAIN"]["HUE_MAP"] = JsonConvert.SerializeObject(HUE_MAP);
                parser.WriteFile(iniPath, data);
            } else {
                loadData();
            }
        }

        public void saveData() {
            var parser = new FileIniDataParser();
            IniData data = parser.ReadFile(iniPath);
            data["MAIN"]["DS_IP"] = DS_IP;
            data["MAIN"]["HUE_IP"] = HUE_IP;
            data["MAIN"]["HUE_SYNC"] = HUE_SYNC ? "True" : "False";
            data["MAIN"]["HUE_AUTH"] = HUE_AUTH ? "True" : "False";
            data["MAIN"]["HUE_KEY"] = HUE_KEY;
            data["MAIN"]["HUE_USER"] = HUE_USER;
            data["MAIN"]["DS_GROUP_NAME"] = DS_GROUP_NAME;
            data["MAIN"]["DS_GROUP_ID"] = DS_GROUP_ID.ToString();
            data["MAIN"]["DS_DEVICE_NAME"] = DS_DEVICE_NAME;
            data["MAIN"]["DS_BRIGHTNESS"] = DS_BRIGHTNESS.ToString();
            data["MAIN"]["DS_SATURATION"] = DS_SATURATION;
            data["MAIN"]["DS_AMBIENT_COLOR"] = DS_AMBIENT_COLOR;
            data["MAIN"]["DS_AMBIENT_MODE_TYPE"] = DS_AMBIENT_MODE_TYPE.ToString();
            data["MAIN"]["DS_AMBIENT_SCENE"] = DS_AMBIENT_SCENE.ToString();
            data["MAIN"]["DS_MODE"] = DS_MODE.ToString();
            data["MAIN"]["HUE_LIGHTS"] = JsonConvert.SerializeObject(HUE_LIGHTS);
            data["MAIN"]["HUE_MAP"] = JsonConvert.SerializeObject(HUE_MAP);
            parser.WriteFile(iniPath, data);
        }

        public bool loadData() {
            var parser = new FileIniDataParser();
            IniData data = parser.ReadFile(iniPath);
            DS_IP = data["MAIN"]["DS_IP"];
            HUE_IP = data["MAIN"]["HUE_IP"];
            HUE_SYNC = data["MAIN"]["HUE_SYNC"] == "True";
            HUE_AUTH = data["MAIN"]["HUE_AUTH"] == "True";
            HUE_KEY = data["MAIN"]["HUE_KEY"];
            HUE_USER = data["MAIN"]["HUE_USER"];
            DS_GROUP_NAME = data["MAIN"]["DS_GROUP_NAME"] ?? "undefined";
            DS_GROUP_ID = int.Parse(data["MAIN"]["DS_GROUP_ID"] ?? "100");
            DS_DEVICE_NAME = data["MAIN"]["DS_DEVICE_NAME"] ?? "HueDream";
            DS_BRIGHTNESS = int.Parse(data["MAIN"]["DS_BRIGHTNESS"] ?? "100");
            DS_SATURATION = data["MAIN"]["DS_SATURATION"] ?? "FFFFFF";
            DS_AMBIENT_COLOR = data["MAIN"]["DS_AMBIENT_COLOR"] ?? "FFFFFF";
            DS_AMBIENT_MODE_TYPE = int.Parse(data["MAIN"]["DS_AMBIENT_MODE_TYPE"] ?? "0");
            DS_MODE = int.Parse(data["MAIN"]["DS_MODE"] ?? "0");
            DS_MODE = int.Parse(data["MAIN"]["DS_AMBIENT_SCENE"] ?? "0");
            HUE_LIGHTS = JsonConvert.DeserializeObject<List<KeyValuePair<int, string>>>(data["MAIN"]["HUE_LIGHTS"]);
            HUE_MAP = JsonConvert.DeserializeObject<List<KeyValuePair<int, string>>>(data["MAIN"]["HUE_MAP"]);
            return false;
        }
    }
}
