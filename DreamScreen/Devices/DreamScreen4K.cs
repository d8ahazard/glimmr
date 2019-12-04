namespace HueDream.DreamScreen.Devices {
    public class DreamScreen4K : DreamScreenHd {
        private const string DeviceTag = "DreamScreen4K";
        private static readonly byte[] Required4KEspFirmwareVersion = {1, 6};
        private static readonly byte[] Required4KPicVersionNumber = {5, 6};

        public DreamScreen4K(string ipAddress) : base(ipAddress) {
            ProductId = 2;
            Name = "DreamScreen 4K";
            Tag = DeviceTag;
            EspFirmwareVersion = Required4KEspFirmwareVersion;
            PicVersionNumber = Required4KPicVersionNumber;
        }
    }
}