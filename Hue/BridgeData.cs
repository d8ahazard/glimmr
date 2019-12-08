using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using HueDream.HueDream;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Q42.HueApi;
using Q42.HueApi.Models.Groups;

namespace HueDream.Hue {
    public class BridgeData {
        public string BridgeIp { get; set; }
        public string BridgeId { get; set; }
        public string BridgeUser { get; set; }
        public string BridgeKey { get; set; }
        public Group EntertainmentGroup { get; set; }
        [JsonProperty]
        private LightMap[] mappedLights;
        [JsonProperty]
        private Group[] entertainmentGroups;

        private Light[] groupLights;

        public BridgeData() {
            BridgeIp = null;
            BridgeId = null;
            BridgeUser = null;
            BridgeKey = null;
            EntertainmentGroup = null;
            mappedLights = Array.Empty<LightMap>();
            entertainmentGroups = Array.Empty<Group>();
        }

        public BridgeData(string ip, string id) {
            BridgeIp = ip;
            BridgeId = id;
        }

        public BridgeData(string ip, string id, string user, string key) {
            BridgeIp = ip;
            BridgeId = id;
            BridgeUser = user;
            BridgeKey = key;
            mappedLights = Array.Empty<LightMap>();
            entertainmentGroups = Array.Empty<Group>();
        }

        public void SetDefaultGroup() {
            var fg = entertainmentGroups.FirstOrDefault();
            if (fg != null) {
                EntertainmentGroup = entertainmentGroups[0];
            }
        }
        
        public void SetMap(LightMap[] map) {
            mappedLights = map;
        }

        public LightMap[] GetMap() {
            return mappedLights;
        }
        
        public void SetLights(Light[] lights) {
            groupLights = lights;
        }

        public Light[] GetLights() {
            return groupLights;
        }

        public void SetGroups(Group[] groups) {
            entertainmentGroups = groups;
            SetDefaultGroup();
        }

        public Group[] GetGroups() {
            return entertainmentGroups;
        }
    }
    
}