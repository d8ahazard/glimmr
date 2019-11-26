using System;

namespace HueDream.DreamScreen.Devices {
    public class DreamScreen4K : DreamScreen {
        private static readonly byte[] required4KEspFirmwareVersion = new byte[] { 1, 6 };
        private static readonly byte[] required4KPicVersionNumber = new byte[] { 5, 6 };
        private const string tag = "DreamScreen4K";

        public DreamScreen4K(string ipAddress) : base(ipAddress) {
            ProductId = 2;
            Name = "DreamScreen 4K";
            Tag = tag;
            EspFirmwareVersion = required4KEspFirmwareVersion;
            PicVersionNumber = required4KPicVersionNumber;
        }
    }
}