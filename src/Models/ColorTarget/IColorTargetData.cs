using System;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using rpi_ws281x;

namespace Glimmr.Models.ColorTarget {
    public interface IColorTargetData {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Tag { get; set; }
        public string IpAddress { get; set; }
        public int Brightness { get; set; }
        public bool Enable { get; set; }
        public string LastSeen { get; set; }
        public void UpdateFromDiscovered(IColorTargetData data);
        [JsonProperty]
        public SettingsProperty[] KeyProperties { get; set; }
    }

    [Serializable]
    public class SettingsProperty {
        [JsonProperty] public string ValueName;
        [JsonProperty] public string ValueType;
        [JsonProperty] public string ValueLabel;
        [JsonProperty] public string ValueMax { get; set; }
        [JsonProperty] public string ValueMin { get; set; }
        [JsonProperty] public string ValueStep { get; set; }
        [JsonProperty] public Dictionary<string, string> Options;

        public SettingsProperty(){}

        public SettingsProperty(string name, string type, string label, Dictionary<string,string> options = null) {
            ValueName = name;
            ValueType = type;
            ValueLabel = label;
            Options = options ?? new Dictionary<string, string>();
        }
    }
}