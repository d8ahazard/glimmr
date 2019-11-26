namespace HueDream.DreamScreen.Devices {
    public interface DreamDevice {
        string Tag { get; set; }
        string Name { get; set; }
        string GroupName { get; set; }
        int GroupNumber { get; set; }
        int Mode { get; set; }
        int[] AmbientColor { get; set; }
        int AmbientModeType { get; set; }
        int[] Saturation { get; set; }
        int Brightness { get; set; }

        void ParsePayload(byte[] payload);

        byte[] EncodeState();
    }
}
