namespace HueDream.HueDream {
    public class LightMap {
        public int LightId { get; }
        public int SectorId { get; }
        public bool OverrideBrightness { get; }
        public int Brightness { get; }

        public LightMap(int lightId, int sectorId, bool doOverride = false, int bright = 100) {
            LightId = lightId;
            SectorId = sectorId;
            OverrideBrightness = doOverride;
            Brightness = bright;
        }
    }
}
