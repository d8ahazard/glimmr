using System;
using System.Collections.Generic;
using HueDream.Models.DreamScreen.Devices;
using HueDream.Models.Util;
using Newtonsoft.Json;

namespace HueDream.Models.DreamScreen {
    [Serializable]
    public class DreamScreenMessage {
        private readonly byte[] payload;

        public DreamScreenMessage(byte[] bytesIn, string from) {
            var byteString = BitConverter.ToString(bytesIn);
            var bytesString = byteString.Split("-");
            var magic = bytesString[0];
            if (!MsgUtils.CheckCrc(bytesIn) || magic != "FC" || bytesString.Length < 7) {
                //throw new ArgumentException($"Invalid message format: {magic}");
                IsValid = false;
                return;
            }

            int len = bytesIn[1];
            Group = bytesIn[2];
            Flags = bytesString[3];
            var cmd = bytesString[4] + bytesString[5];
            if (MsgUtils.Commands.ContainsKey(cmd))
                Command = MsgUtils.Commands[cmd];
            else
                Console.WriteLine($@"DSMessage: No matching key in dict for bytes: {cmd}.");
            BaseDevice dreamDev = null;
            if (len > 5) {
                payload = ExtractPayload(bytesIn);
                PayloadString = payload.Length != 0
                    ? BitConverter.ToString(payload).Replace("-", string.Empty, StringComparison.CurrentCulture)
                    : "";
            }

            if (Command == "DEVICE_DISCOVERY" && Flags == "60" && len > 46) {
                int devType = payload[^1];
                switch (devType) {
                    case 1:
                        dreamDev = new DreamScreenHd(from);
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
                    dreamDev.Id = from;
                }
                else {
                    Console.WriteLine($@"DSMessage: Device is null from {devType}.");
                }

                Device = dreamDev;
                payload = null;
            }

            IsValid = true;
        }

        public string Command { get; }
        public int Group { get; }
        public string Flags { get; }
        public string PayloadString { get; }

        [JsonProperty] public string IpAddress { get; set; }

        public bool IsValid { get; }
        public BaseDevice Device { get; }

        public byte[] GetPayload() {
            return payload;
        }

        private static byte[] ExtractPayload(byte[] source) {
            var i = 0;
            var output = new List<byte>();
            foreach (var b in source) {
                if (i > 5 && i < source.Length - 1) output.Add(b);
                i++;
            }

            return output.ToArray();
        }
    }
}