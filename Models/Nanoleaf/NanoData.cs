using Newtonsoft.Json;
using System;
using System.Drawing;

namespace HueDream.Models.Nanoleaf {
    [Serializable]
    public class NanoData {
        [JsonProperty] public const string Tag = "NanoLeaf";
        [JsonProperty] public string IpV4Address { get; set; }
        [JsonProperty] public string IpV6Address { get; set; }
        [JsonProperty] public string Hostname { get; set; }
        [JsonProperty] public string Id { get; set; }
        [JsonProperty] public int Port { get; set; }
        [JsonProperty] public string GroupName { get; set; }
        [JsonProperty] public string Token { get; set; }
        [JsonProperty] public int GroupNumber { get; set; }
        [JsonProperty] public static string Name { get; set; }
        [JsonProperty] public string Type { get; set; }
        [JsonProperty] public string Version { get; set; }
        [JsonProperty] public int Mode { get; set; }
        [JsonProperty] public NanoLayout Layout { get; set; }
        [JsonProperty] public Point CenterPoint { get; set; }
        
        [JsonProperty] public int X { get; set; }
        [JsonProperty] public int Y { get; set; }
        [JsonProperty] public float Scale { get; set; }
        [JsonProperty] public float Rotation { get; set; }
}
}
