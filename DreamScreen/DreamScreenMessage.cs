using HueDream.DreamScreen.Devices;
using HueDream.Util;
using System;
using System.Collections.Generic;

namespace HueDream.DreamScreen {

    [Serializable]
    public class DreamScreenMessage {
        public string command { get; set; }
        public string addr { get; }
        public string flags { get; }
        public string upper { get; }
        public string lower { get; }
        public byte[] payload { get; }
        public string[] payloadString { get; set; }
        public string hex { get; }

        public string ipAddress;

        public bool IsValid { get; set; }

        
        public BaseDevice device { get; set; }


        public DreamScreenMessage(byte[] bytesIn, string from) {
            string byteString = BitConverter.ToString(bytesIn);
            string[] bytesString = byteString.Split("-");
            string magic = bytesString[0];
            if (!MsgUtils.CheckCrc(bytesIn) || magic != "FC") {
                Console.WriteLine("CRC Failed.");
                IsValid = false;
                return;
            }
            hex = string.Join("", bytesString);
            int len = bytesIn[1];
            addr = bytesString[2];
            flags = bytesString[3];
            upper = bytesString[4];
            lower = bytesString[5];
            string cmd = bytesString[4] + bytesString[5];
            if (MsgUtils.commands.ContainsKey(cmd)) {
                command = MsgUtils.commands[cmd];
            } else {
                Console.WriteLine("No matching key in dict for bytes: " + cmd);
            }

            BaseDevice dreamDev = null;
            if (len > 5) {
                payload = Payload(bytesIn);
                string pString = BitConverter.ToString(payload);
                payloadString = pString.Split("-");
            }
            if (command == "DEVICE_DISCOVERY" && flags == "60" && len > 46) {
                string typeByte = payloadString[payloadString.Length - 1];
                Console.WriteLine("In Byte: " + typeByte);
                switch (typeByte) {
                    case "01":
                        dreamDev = new Devices.DreamScreen(from);
                        break;
                    case "02":
                        dreamDev = new DreamScreen4K(from);
                        break;
                    case "03":
                        dreamDev = new SideKick(from);
                        break;
                    case "04":
                        dreamDev = new Connect(from);
                        break;
                    case "07":
                        dreamDev = new DreamScreenSolo(from);
                        break;
                }
                if (dreamDev != null) {
                    dreamDev.Initialize();
                    dreamDev.ParsePayload(payload);
                } else {
                    Console.WriteLine("DreamDev is null.");
                }
                device = dreamDev;
                payload = null;
            }
            IsValid = true;
        }

        private static byte[] Payload(byte[] source) {
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
