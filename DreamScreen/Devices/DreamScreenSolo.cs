namespace HueDream.DreamScreen.Devices {

    public class DreamScreenSolo : DreamScreenHd {
        private static readonly byte[] RequiredSoloEspFirmwareVersion = { 1, 6 };
        private static readonly byte[] RequiredSoloPicVersionNumber = { 6, 2 };
        private const string DeviceTag = "DreamScreenSolo";

        public DreamScreenSolo(string ipAddress) : base(ipAddress) {
            ProductId = 7;
            Name = "DreamScreen Solo";
            Tag = DeviceTag;
            EspFirmwareVersion = RequiredSoloEspFirmwareVersion;
            PicVersionNumber = RequiredSoloPicVersionNumber;
        }
    }

}