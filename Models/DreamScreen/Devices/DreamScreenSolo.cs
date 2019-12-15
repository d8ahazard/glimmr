namespace HueDream.DreamScreen.Devices {
    public class DreamScreenSolo : DreamScreenHd {
        private const string DeviceTag = "DreamScreenSolo";
        private static readonly byte[] RequiredSoloEspFirmwareVersion = {1, 6};
        private static readonly byte[] RequiredSoloPicVersionNumber = {6, 2};

        public DreamScreenSolo(string ipAddress) : base(ipAddress) {
            ProductId = 7;
            Name = "DreamScreen Solo";
            Tag = DeviceTag;
            EspFirmwareVersion = RequiredSoloEspFirmwareVersion;
            PicVersionNumber = RequiredSoloPicVersionNumber;
        }
    }
}