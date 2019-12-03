namespace HueDream.DreamScreen.Devices {

    public class DreamScreenSolo : DreamScreenHD {
        private static readonly byte[] requiredSoloEspFirmwareVersion = new byte[] { 1, 6 };
        private static readonly byte[] requiredSoloPicVersionNumber = new byte[] { 6, 2 };
        private const string tag = "DreamScreenSolo";

        public DreamScreenSolo(string ipAddress) : base(ipAddress) {
            ProductId = 7;
            Name = "DreamScreen Solo";
            Tag = tag;
            EspFirmwareVersion = requiredSoloEspFirmwareVersion;
            PicVersionNumber = requiredSoloPicVersionNumber;
        }
    }

}