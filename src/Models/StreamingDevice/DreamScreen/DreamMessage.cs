using System;
using System.Collections.Generic;
using Glimmr.Models.Util;
using Newtonsoft.Json;
using Serilog;
using Connect = Glimmr.Models.StreamingDevice.Dreamscreen.Encoders.Connect;
using SideKick = Glimmr.Models.StreamingDevice.Dreamscreen.Encoders.SideKick;

namespace Glimmr.Models.StreamingDevice.Dreamscreen {
    [Serializable]
    public class DreamscreenMessage {
        public string Command { get; }
        public byte C1 { get; }
        public byte C2 { get; }
        public int Group { get; }
        public string Flags { get; }
        public string PayloadString { get; }

        [JsonProperty] public string IpAddress { get; set; }

        public bool IsValid { get; }
        
        public int Len { get; set; }
        public DreamData Device { get; }

        private readonly byte[] _payload;
        
        

        public DreamscreenMessage(byte[] bytesIn, string from) {
            IpAddress = from;
            var byteString = BitConverter.ToString(bytesIn);
            var bytesString = byteString.Split("-");
            var magic = bytesString[0];
            C1 = bytesIn[4];
            C2 = bytesIn[5];

            if (!MsgUtils.CheckCrc(bytesIn) || magic != "FC" || bytesString.Length < 7) {
                if (C1 != 5 || C2 != 22) {
                    IsValid = false;
                    return;    
                }
            }

            Len = bytesIn[1];
            Group = bytesIn[2];
            Flags = bytesString[3];
            C1 = bytesIn[4];
            C2 = bytesIn[5];
            var cmd = bytesString[4] + bytesString[5];

            if (MsgUtils.Commands.ContainsKey(cmd)) {
                Command = MsgUtils.Commands[cmd];
                if (Command == "MINIMUM_LUMINOSITY") Log.Debug("BYTE STRING: " + byteString);

            } else {
                Log.Debug($@"DSMessage: No matching key in dict for bytes: {cmd}.");
            }

            var dd = new DreamData();
            if (Len > 5) {
                _payload = ExtractPayload(bytesIn);
                PayloadString = _payload.Length != 0
                    ? BitConverter.ToString(_payload).Replace("-", string.Empty, StringComparison.CurrentCulture)
                    : "";
            }
            
            if (Command == "DEVICE_DISCOVERY" && Flags == "60" && Len > 46) {
                int devType = _payload[^1];
                switch (devType) {
                    case 1:
                    case 2:
                    case 7:
                        dd = Encoders.Dreamscreen.ParsePayload(_payload);
                        break;
                    case 3:
                        dd = SideKick.ParsePayload(_payload);
                        break;
                    case 4:
                        dd = Connect.ParseePayload(_payload);
                        break;
                }

                if (dd != null) {
                    dd.Id = from;
                    dd.IpAddress = from;
                }
                else {
                    Log.Warning($@"DSMessage: Device is null from {devType}.");
                }

                Device = dd;
                _payload = null;
            }

            IsValid = true;
        }

        
        public byte[] GetPayload() {
            return _payload;
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