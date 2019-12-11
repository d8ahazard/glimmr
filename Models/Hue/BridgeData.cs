using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Q42.HueApi.Models.Groups;

namespace HueDream.Models.Hue {
    [Serializable]
    public class BridgeData {
        public BridgeData() { }

        public BridgeData(string ip, string id) {
            Ip = ip;
            Id = id;
        }

        public BridgeData(string ip, string id, string user, string key, string group = "-1") {
            Ip = ip;
            Id = id;
            User = user;
            Key = key;
            SelectedGroup = group;
            Groups = new List<Group>();
            Lights = new List<LightData>();
        }

        [JsonProperty] public string Ip { get; set; }

        [JsonProperty] public string Id { get; set; }

        [JsonProperty] public string User { get; set; }

        [JsonProperty] public string Key { get; set; }

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
            var bridgeId = string.Empty;
            var bridgeIp = string.Empty;
            var bridgeUser = string.Empty;
            var bridgeKey = string.Empty;
            var selectedGroup = "-1";
            var bridgeGroups = Array.Empty<Group>();
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
                            Console.Write(@"Parsed lights: " + JsonConvert.SerializeObject(bridgeLights));
                        }
                        finally {
                            Console.Write(@"Light parse exception.");
                        }

                        break;
                    case "selectedGroup":
                        selectedGroup = (string) property.Value;
                        Console.WriteLine(@"Group is " + selectedGroup);
                        break;
                    case "groups":
                        try {
                            bridgeGroups = property.Value.ToObject<Group[]>();
                            Console.WriteLine(@"Deserialized groups: " + JsonConvert.SerializeObject(bridgeGroups));
                        }
                        finally {
                            Console.WriteLine(@"Cast exception for group.");
                        }

                        break;
                }

            var bd = new BridgeData(bridgeIp, bridgeId) {
                Groups = bridgeGroups.ToList(),
                Key = bridgeKey,
                User = bridgeUser,
                SelectedGroup = selectedGroup,
                Lights = bridgeLights
            };
            Console.WriteLine(@"Returning bridge data item: " + JsonConvert.SerializeObject(bd));
            return bd;
        }
    }
}