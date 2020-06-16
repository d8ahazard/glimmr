using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Q42.HueApi.Models.Bridge;
using Q42.HueApi.Models.Groups;

namespace HueDream.Models.StreamingDevice.Hue {
    [Serializable]
    public class BridgeData {
        public BridgeData() { }

        public BridgeData(string ip, string id) {
            IpAddress = ip;
            Id = id;
        }

        public BridgeData(LocatedBridge b) {
            if (b == null) throw new ArgumentException("Invalid located bridge.");
            IpAddress = b.IpAddress;
            Id = b.BridgeId;
        }

        public BridgeData(string ip, string id, string user, string key, string group = "-1", string groupName = "undefined", int groupNumber = 0) {
            IpAddress = ip;
            Id = id;
            User = user;
            Key = key;
            SelectedGroup = group;
            Groups = new List<Group>();
            Lights = new List<LightData>();
            GroupName = groupName;
            GroupNumber = groupNumber;
        }

        public void CopyBridgeData(BridgeData existing) {
            if (existing == null) throw new ArgumentException("Invalid bridge data.");
            Key = existing.Key;
            User = existing.User;
            Lights = existing.Lights;
            Groups = existing.Groups;
            SelectedGroup = existing.SelectedGroup;
            Brightness = existing.Brightness;
        }

        [JsonProperty] public string IpAddress { get; set; }
        [JsonProperty] public string Id { get; set; }
        [JsonProperty] public string User { get; set; }
        [JsonProperty] public string Key { get; set; }
        [JsonProperty] public static string Tag = "HueBridge";
        [JsonProperty] public string Name { get; set; }
        [JsonProperty] public string GroupName { get; set; }
        [JsonProperty] public int GroupNumber { get; set; }
        
        [DefaultValue(100)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int Brightness { get; set; }
        [JsonProperty] public string SelectedGroup { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<Group> Groups { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<LightData> Lights { get; set; }


        public void SetLights(List<LightData> lights) {
            Lights = lights;
        }

        public List<LightData> GetLights() {
            return Lights ??= new List<LightData>();
        }

        public void SetGroups(List<Group> groups) {
            Groups = groups;
        }

        public List<Group> GetGroups() {
            return Groups ??= new List<Group>();
        }

        public static BridgeData DeserializeBridgeData(JObject o) {
            if (o == null) throw new ArgumentNullException(nameof(o));
            var bridgeId = string.Empty;
            var bridgeIp = string.Empty;
            var bridgeUser = string.Empty;
            var bridgeKey = string.Empty;
            var selectedGroup = "-1";
            var groupNumber = 0;
            var name = "Hue Bridge";
            var groupName = "undefined";
            var bridgeGroups = Array.Empty<Group>();
            var groupIds = new List<string>();
            var bridgeLights = new List<LightData>();
            Console.WriteLine(@"Deserializing bridge...");
            foreach (var property in o.Properties())
                switch (property.Name) {
                    case "ip":
                        bridgeIp = (string) property.Value;
                        break;
                    case "id":
                        bridgeId = (string) property.Value;
                        break;
                    case "user":
                        bridgeUser = (string) property.Value;
                        break;
                    case "key":
                        bridgeKey = (string) property.Value;
                        break;
                    case "lights":
                        try {
                            bridgeLights = property.Value.ToObject<List<LightData>>();
                            //Console.Write(@"Parsed lights: " + JsonConvert.SerializeObject(bridgeLights));
                        }
                        finally {
                            Console.Write(@"Light parse exception.");
                        }

                        break;
                    case "selectedGroup":
                        selectedGroup = (string) property.Value;
                        Console.WriteLine(@"Selected Group is " + selectedGroup);
                        break;
                    case "groups":
                        try {
                            bridgeGroups = property.Value.ToObject<Group[]>();
                        } catch {
                            Console.WriteLine(@"Cast exception for group.");
                        }
                        break;
                    case "groupNumber":
                        groupNumber = (int) property.Value;
                        break;
                    case "groupName":
                        groupName = (string)property.Value;
                        break;
                }

            var bd = new BridgeData(bridgeIp, bridgeId) {
                Groups = bridgeGroups.ToList(),
                Key = bridgeKey,
                User = bridgeUser,
                SelectedGroup = selectedGroup,
                Lights = bridgeLights,
                GroupName = groupName,
                GroupNumber = groupNumber,
                Name = name,
                Id = bridgeId
        };
            if (bd.Groups.Count > 0) {
                groupIds.AddRange(bd.Groups.Select(g => g.Id));
                if (!groupIds.Contains(selectedGroup) || selectedGroup == "-1") {
                    bd.SelectedGroup = groupIds[0];
                }
            }
            
            Console.WriteLine(@"Returning bridge data item: " + JsonConvert.SerializeObject(bd));
            return bd;
        }
    }
}