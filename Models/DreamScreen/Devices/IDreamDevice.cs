using Newtonsoft.Json;

namespace HueDream.Models.DreamScreen.Devices {
    public interface IDreamDevice {
        [JsonProperty] string Tag { get; set; }

        [JsonProperty] string Name { get; set; }

        [JsonProperty] string AmbientColor { get; set; }

        [JsonProperty] string Saturation { get; set; }

        [JsonProperty] string GroupName { get; set; }

        [JsonProperty] int GroupNumber { get; set; }

        [JsonProperty] int Mode { get; set; }
        [JsonProperty] public int[] flexSetup { get; set; }

        [JsonProperty] int AmbientModeType { get; set; }

        [JsonProperty] int Brightness { get; set; }

        void ParsePayload(byte[] payload);

        byte[] EncodeState();
    }
}