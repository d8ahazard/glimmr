using HueDream.DreamScreen.Devices;
using HueDream.Hue;
using Q42.HueApi.Models.Groups;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;

namespace HueDream.HueDream {

    [Serializable]
    public class DataObj {

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
        
        [JsonProperty] private Group[] entertainmentGroups;

        private void SetEntertainmentGroups(Group[] value) {
            entertainmentGroups = value;
        }

        [JsonProperty]
        private BaseDevice[] devices;

        private void SetDevices(BaseDevice[] value) {
            devices = value;
        }

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

        private static string GetLocalIpAddress() {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList) {
                if (ip.AddressFamily == AddressFamily.InterNetwork) {
                    return ip.ToString();
                }
            }
            return "localhost";
        }
    }
}
