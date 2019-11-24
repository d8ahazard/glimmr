using HueDream.DreamScreen;
using HueDream.Hue;
using Q42.HueApi.Models.Groups;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HueDream.HueDream {

    [Serializable]
    public class DataObj {

        private DreamState dreamState;
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
        public DreamState DreamState { get => dreamState; set => dreamState = value; }

        public DataObj() {
            DsIp = null;
            DreamState = new DreamState();
            HueIp = HueBridge.findBridge();
            HueSync = false;
            HueAuth = false;
            HueKey = "";
            HueUser = "";
            HueLights = new List<KeyValuePair<int, string>>();
            HueMap = new List<KeyValuePair<int, int>>();
            EntertainmentGroups = null;
            EntertainmentGroup = null;
        }

    }
}
