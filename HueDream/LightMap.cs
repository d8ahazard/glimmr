namespace HueDream.HueDream {
    public class LightMap {
        public int LightId { get; set; }
        public int SectorId { get; set; }
        public bool OverrideBrightness { get; set; }
        public int Brightness { get; set; }


        public LightMap(int lightId, int sectorId, bool doOverride = false, int bright = 100) {
            LightId = lightId;
            SectorId = sectorId;
            OverrideBrightness = doOverride;
            Brightness = bright;
        }
    }
}
