namespace HueDream.DreamScreen.Devices {
    public interface IDreamDevice {
        string Tag { get; set; }
        string Name { get; set; }
        string AmbientColor { get; set; }
        string Saturation { get; set; }
        string GroupName { get; set; }
        int GroupNumber { get; set; }
        int Mode { get; set; }




        int AmbientModeType { get; set; }
        int Brightness { get; set; }

        void ParsePayload(byte[] payload);

        byte[] EncodeState();
    }
}
