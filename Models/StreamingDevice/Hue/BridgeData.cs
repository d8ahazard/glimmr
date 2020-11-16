using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Q42.HueApi.Models.Bridge;
using Q42.HueApi.Models.Groups;

namespace Glimmr.Models.StreamingDevice.Hue {
    [Serializable]
    public class BridgeData : StreamingData {
        public BridgeData() {
            Tag = "HueBridge";
        }

        public BridgeData(string ip, string id) {
            IpAddress = ip;
            Id = id;
            Brightness = 100;
            Tag = "HueBridge";
            Name = "HueBridge - " + id.Substring(0, 4);
        }

        public BridgeData(LocatedBridge b) {
            if (b == null) throw new ArgumentException("Invalid located bridge.");
            IpAddress = b.IpAddress;
            Id = b.BridgeId;
            Brightness = 100;
            Name = "Hue Bridge - " + Id.Substring(0, 4);
            User = "";
            Key = "";
            SelectedGroup = "-1";
            Groups = new List<Group>();
            Lights = new List<LightData>();
            GroupName = "";
            GroupNumber = -1;
            Tag = "HueBridge";
        }

        public BridgeData(string ip, string id, string user, string key, string group = "-1", string groupName = "undefined", int groupNumber = 0) {
            Name = "Hue Bridge - " + Id.Substring(0, 4);
            IpAddress = ip;
            Id = id;
            User = user;
            Key = key;
            SelectedGroup = group;
            Groups = new List<Group>();
            Lights = new List<LightData>();
            GroupName = groupName;
            GroupNumber = groupNumber;
            Tag = "HueBridge";
        }

        public void CopyBridgeData(BridgeData existing) {
            if (existing == null) throw new ArgumentException("Invalid bridge data.");
            Key = existing.Key;
            User = existing.User;
            var cl = new List<LightData>();
            foreach (var l in existing.Lights.Where(l => l.Id != null)) {
                foreach (var el in Lights.Where(el => el.Id == l.Id)) {
                    l.TargetSector = el.TargetSector;
                    l.TargetSectorV2 = el.TargetSectorV2;
                    l.Brightness = el.Brightness;
                    l.OverrideBrightness = el.OverrideBrightness;
                }
                cl.Add(l);
            }

            foreach (var el in Lights) {
                var added = false;
                foreach (var l in cl) {
                    if (l.Id == el.Id) {
                        added = true;
                    }
                }
                if (!added) cl.Add(el);
            }
            Lights = existing.Lights;
            Groups = existing.Groups;
            Name = "Hue Bridge - " + existing.Id.Substring(0, 4);
            SelectedGroup = existing.SelectedGroup;
            Brightness = existing.Brightness;
        }

        [JsonProperty] public string User { get; set; }
        [JsonProperty] public string Key { get; set; }
        [JsonProperty] public string GroupName { get; set; }
        [JsonProperty] public int GroupNumber { get; set; }
        [JsonProperty] public string SelectedGroup { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<Group> Groups { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<LightData> Lights { get; set; }

    }
}