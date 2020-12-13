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
        [JsonProperty] public string AmbientColor { get; set; }
        [JsonProperty] public bool DefaultSet { get; set; }
        [JsonProperty] public bool ShowSource { get; set; }
        [JsonProperty] public bool AutoDisabled { get; set; }
        [JsonProperty] public bool ShowEdged { get; set; }
        [JsonProperty] public bool ShowWarped { get; set; }
        [JsonProperty] public float ScaleFactor { get; set; }
        [JsonProperty] public float AudioThreshold { get; set; }
        [JsonProperty] public int Sensitivity { get; set; }

        [JsonProperty] public int CamWidth { get; set; }
        [JsonProperty] public int CamHeight { get; set; }
        [JsonProperty] public int CamType { get; set; }
        [JsonProperty] public int K { get; set; }
        [JsonProperty] public int D { get; set; }
        [JsonProperty] public int CaptureMode { get; set; }
        [JsonProperty] public int MinBrightness { get; set; }
        [JsonProperty] public int SaturationBoost { get; set; }

        [JsonProperty] public int RecId { get; set; }
        [JsonProperty] public string DevType { get; set; }
        [JsonProperty] public string DsIp { get; set; }
        [JsonProperty] public string RecDev { get; set; }

        

        public SystemData(bool setDefaults = false) {
            if (setDefaults) {
                DeviceMode = 0;
                AmbientMode = 0;
                AmbientShow = 0;
                AmbientColor = "FFFFFF";
                RecId = 1;
                DevType = "Dreamscreen4K";
                CamWidth = 1920;
                CamHeight = 1080;
                CamType = 1;
                ScaleFactor = .5f;
                CaptureMode = 2;
                MinBrightness = 255;
                SaturationBoost = 0;
                DsIp = IpUtil.GetLocalIpAddress();
                AudioThreshold = .01f;
                DefaultSet = true;
            }
        }
        
        private static string GetLocalIpAddress() {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList) {
                if (ip.AddressFamily == AddressFamily.InterNetwork) {
                    return ip.ToString();
                }
            }

            return "127.0.0.1";
        }
    }
}