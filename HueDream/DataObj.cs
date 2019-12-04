using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using HueDream.DreamScreen.Devices;
using HueDream.Hue;
using Newtonsoft.Json;
using Q42.HueApi.Models.Groups;

namespace HueDream.HueDream {
    [Serializable]
    public class DataObj {
        [JsonProperty] private BaseDevice[] devices;

        [JsonProperty] private Group[] entertainmentGroups;

        public DataObj() {
            DsIp = "0.0.0.0";
            MyDevice = new SideKick(GetLocalIpAddress());
            HueIp = HueBridge.FindBridge();
            HueSync = false;
            HueAuth = false;
            HueKey = "";
            HueUser = "";
            EmuType = "SideKick";
            HueLights = new List<KeyValuePair<int, string>>();
            HueMap = new List<LightMap>();
            SetEntertainmentGroups(null);
            SetDevices(Array.Empty<BaseDevice>());
            MyDevice.Initialize();
        }

        [JsonProperty] public BaseDevice MyDevice { get; set; }

        //public BaseDevice[] MyDevices { get; set; }
        [JsonProperty] public string DsIp { get; set; }
        [JsonProperty] public string HueIp { get; set; }
        [JsonProperty] public string EmuType { get; set; }
        [JsonProperty] public bool HueSync { get; set; }
        [JsonProperty] public bool HueAuth { get; set; }
        [JsonProperty] public string HueKey { get; set; }
        [JsonProperty] public string HueUser { get; set; }
        [JsonProperty] public List<KeyValuePair<int, string>> HueLights { get; }
        [JsonProperty] public List<LightMap> HueMap { get; }

        private void SetEntertainmentGroups(Group[] value) {
            entertainmentGroups = value;
        }

        private void SetDevices(BaseDevice[] value) {
            devices = value;
        }

        private static string GetLocalIpAddress() {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
            return "localhost";
        }
    }
}