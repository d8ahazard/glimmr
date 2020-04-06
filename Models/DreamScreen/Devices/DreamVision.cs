namespace HueDream.Models.DreamScreen.Devices {
    public class DreamVision : DreamScreenHd {
        private const string DeviceTag = "DreamScreen4K";
        private static readonly byte[] Required4KEspFirmwareVersion = {1, 6};
        private static readonly byte[] Required4KPicVersionNumber = {5, 6};
    }
}