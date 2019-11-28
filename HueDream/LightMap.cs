namespace HueDream.HueDream {
    public class LightMap {
        public int LightId { get; set; }
        public int SectorId { get; set; }

        public LightMap(int lightId, int sectorId) {
            LightId = lightId;
            SectorId = sectorId;
        }
    }
}
