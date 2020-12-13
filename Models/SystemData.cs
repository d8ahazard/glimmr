using System.Net;
using System.Net.Sockets;
using Glimmr.Models.Util;
using Newtonsoft.Json;
namespace Glimmr.Models {
    public class SystemData {
        [JsonProperty] public int DeviceMode { get; set; }
        [JsonProperty] public int DeviceGroup { get; set; }
        [JsonProperty] public int AmbientMode { get; set; }
        [JsonProperty] public int AmbientShow { get; set; }
        [JsonProperty] public string AmbientColor { get; set; } = "000000";
        [JsonProperty] public bool DefaultSet { get; set; }
        [JsonProperty] public bool ShowSource { get; set; }
        [JsonProperty] public bool AutoDisabled { get; set; }
        [JsonProperty] public bool ShowEdged { get; set; }
        [JsonProperty] public bool ShowWarped { get; set; }
        [JsonProperty] public float AudioThreshold { get; set; } = .01f;
        [JsonProperty] public int Sensitivity { get; set; }

        [JsonProperty] public int CamType { get; set; } = 1;
        [JsonProperty] public int CaptureMode { get; set; } = 2;
        [JsonProperty] public int MinBrightness { get; set; } = 255;
        [JsonProperty] public int SaturationBoost { get; set; }

        [JsonProperty] public int RecId { get; set; } = 1;
        [JsonProperty] public string DevType { get; set; } = "Dreamscreen4K";
        [JsonProperty] public string DsIp { get; set; }
        [JsonProperty] public string RecDev { get; set; }

        

        public SystemData(bool setDefaults = false) {
            if (setDefaults) {
                DefaultSet = true;
            }
        }
    }
}