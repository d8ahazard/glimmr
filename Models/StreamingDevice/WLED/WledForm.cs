using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace HueDream.Models.StreamingDevice.WLed {
    public class WledForm    {
        [JsonProperty][JsonPropertyName("LC")] public string LedCount { get; set; } 
        [JsonProperty][JsonPropertyName("ABen")]public string AutoBrightness { get; set; } 
        [JsonProperty][JsonPropertyName("MA")]public string MaxCurrent { get; set; } 
        [JsonProperty][JsonPropertyName("LAsel")]public string LedVoltage { get; set; } 
        [JsonProperty][JsonPropertyName("LA")]public string CustomLedVoltage { get; set; } 
        [JsonProperty][JsonPropertyName("EW")]public string IsRgbw { get; set; } 
        [JsonProperty][JsonPropertyName("AW")]public string AutoCalculateWhite { get; set; } 
        [JsonProperty][JsonPropertyName("CO")]public string ColorOrder { get; set; } 
        [JsonProperty][JsonPropertyName("BO")]public string AutoEnable { get; set; } 
        [JsonProperty][JsonPropertyName("CA")]public string DefaultBrightness { get; set; } 
        [JsonProperty][JsonPropertyName("BP")]public string BootPreset { get; set; }
        [JsonProperty][JsonPropertyName("PC")]public string PresetCycle { get; set; }
        [JsonProperty][JsonPropertyName("GC")]public string GammaCorrection { get; set; }
        [JsonProperty][JsonPropertyName("GB")]public string GammaBrightness { get; set; }
        [JsonProperty][JsonPropertyName("BF")]public string BrightnessFactor { get; set; } 
        [JsonProperty][JsonPropertyName("TF")]public string FadeTransitions { get; set; } 
        [JsonProperty][JsonPropertyName("TD")]public string TransitionTime { get; set; }
        [JsonProperty][JsonPropertyName("PF")]public string PaletteTransitions { get; set; }
        [JsonProperty][JsonPropertyName("TL")]public string TimedLightDuration { get; set; } 
        [JsonProperty][JsonPropertyName("TB")]public string TargetBrightness { get; set; } 
        [JsonProperty][JsonPropertyName("TW")]public string TimedLightMode { get; set; } 
        [JsonProperty][JsonPropertyName("PB")]public string PaleteBlending { get; set; }
        [JsonProperty][JsonPropertyName("RV")]public string ReverseOrder { get; set; }
        [JsonProperty][JsonPropertyName("SL")]public string SkipFirst { get; set; }
    }

}