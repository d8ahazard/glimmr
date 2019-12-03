using HueDream.DreamScreen.Devices;
using HueDream.Util;
using System;
using System.Collections.Generic;

namespace HueDream.DreamScreen {

    [Serializable]
    public class DreamScreenMessage {
        public string Command { get; set; }
        public string Group { get; }
        public string Flags { get; }
        public string Upper { get; }
        public string Lower { get; }

        private readonly byte[] payload;

        public byte[] GetPayload() {
            return payload;
        }

        public string PayloadString { get; set; }

        public string Hex { get; }
        public string IpAddress { get; set; }

        public bool IsValid { get; set; }


        public BaseDevice device { get; set; }


        public DreamScreenMessage(byte[] bytesIn, string from) {
            string byteString = BitConverter.ToString(bytesIn);
            string[] bytesString = byteString.Split("-");
            string magic = bytesString[0];
            if (!MsgUtils.CheckCrc(bytesIn) || magic != "FC") {
                throw new ArgumentException($"Invalid message format: {magic}");
            }
            Hex = string.Join("", bytesString);
            int len = bytesIn[1];
            Group = bytesString[2];
            Flags = bytesString[3];
            Upper = bytesString[4];
            Lower = bytesString[5];
            string cmd = bytesString[4] + bytesString[5];
            if (MsgUtils.commands.ContainsKey(cmd)) {
                Command = MsgUtils.commands[cmd];
            } else {
                Console.WriteLine("DSMessage: No matching key in dict for bytes: " + cmd);
            }

            BaseDevice dreamDev = null;
            if (len > 5) {
                payload = ExtractPayload(bytesIn);
                PayloadString = BitConverter.ToString(GetPayload()).Replace("-", string.Empty);
            }
            if (Command == "DEVICE_DISCOVERY" && Flags == "60" && len > 46) {
                int devType = payload[payload.Length - 2];
                switch (devType) {
                    case 1:
                        dreamDev = new Devices.DreamScreenHD(from);
                        break;
                    case 2:
                        dreamDev = new DreamScreen4K(from);
                        break;
                    case 3:
                        dreamDev = new SideKick(from);
                        break;
                    case 4:
                        dreamDev = new Connect(from);
                        break;
                    case 7:
                        dreamDev = new DreamScreenSolo(from);
                        break;
                }
                if (dreamDev != null) {
                    dreamDev.Initialize();
                    dreamDev.ParsePayload(GetPayload());
                } else {
                    Console.WriteLine($"DSMessage: Device is null from {devType}.");
                }
                device = dreamDev;
                payload = null;
            }
            IsValid = true;
        }

        private static byte[] ExtractPayload(byte[] source) {
            int i = 0;
            List<byte> output = new List<byte>();
            foreach (byte b in source) {
                if (i > 5 && i < source.Length - 1) {
                    output.Add(b);
                }
                i++;
            }
            return output.ToArray();
        }


    }
}
