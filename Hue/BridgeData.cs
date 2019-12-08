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
        public Group SelectedGroup { get; set; }
        [JsonProperty]
        private LightMap[] mappedLights;
        [JsonProperty]
        private Group[] bridgeGroups;
        [JsonProperty]
        private LightData[] bridgeLights;

        public BridgeData() {
            BridgeIp = null;
            BridgeId = null;
            BridgeUser = null;
            BridgeKey = null;
            SelectedGroup = null;
            mappedLights = Array.Empty<LightMap>();
            bridgeGroups = Array.Empty<Group>();
            bridgeLights = Array.Empty<LightData>();
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
            bridgeGroups = Array.Empty<Group>();
            bridgeLights = Array.Empty<LightData>();
        }

        public void SetDefaultGroup() {
            var fg = bridgeGroups.FirstOrDefault();
            if (fg != null) {
                SelectedGroup = bridgeGroups[0];
            }
        }
        
        public void SetMap(LightMap[] map) {
            mappedLights = map;
        }

        public LightMap[] GetMap() {
            return mappedLights;
        }
        
        public void SetLights(LightData[] lights) {
            bridgeLights = lights;
        }

        public LightData[] GetLights() {
            return bridgeLights;
        }

        public void SetGroups(Group[] groups) {
            bridgeGroups = groups;
            SetDefaultGroup();
        }

        public Group[] GetGroups() {
            return bridgeGroups;
        }
    }
    
}