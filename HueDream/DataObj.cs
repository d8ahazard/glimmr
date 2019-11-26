using HueDream.DreamScreen;
using HueDream.DreamScreen.Devices;
using HueDream.Hue;
using Q42.HueApi.Models.Groups;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace HueDream.HueDream {

    [Serializable]
    public class DataObj {

        public BaseDevice MyDevice { get; set; }
        public string DsIp { get; set; }
        public string HueIp { get; set; }
        public bool HueSync { get; set; }
        public bool HueAuth { get; set; }
        public string HueKey { get; set; }
        public string HueUser { get; set; }
        public List<KeyValuePair<int, string>> HueLights { get; set; }
        public List<KeyValuePair<int, int>> HueMap { get; set; }
        public Group[] EntertainmentGroups { get; set; }
        public Group EntertainmentGroup { get; set; }
        
        private BaseDevice[] devices;

        public BaseDevice[] GetDevices() {
            return devices;
        }

        public void SetDevices(BaseDevice[] value) {
            devices = value;
        }

        public DataObj() {
            DsIp = "0.0.0.0";
            MyDevice = new SideKick(GetLocalIPAddress());
            HueIp = HueBridge.findBridge();
            HueSync = false;
            HueAuth = false;
            HueKey = "";
            HueUser = "";
            HueLights = new List<KeyValuePair<int, string>>();
            HueMap = new List<KeyValuePair<int, int>>();
            EntertainmentGroups = null;
            EntertainmentGroup = null;
            SetDevices(Array.Empty<BaseDevice>());
            MyDevice.Initialize();
        }

        private static string GetLocalIPAddress() {
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
