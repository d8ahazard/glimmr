using System;
using System.Collections.Generic;
using System.IO.Compression;
using HueDream.Models.Util;
using LifxNet;

namespace HueDream.Models.LIFX {
    public class LifxBulb {
        private LifxData Data { get; }
        private LightBulb B { get; }
        private readonly int targetSector;
        public LifxBulb(LifxData d) {
            Data = d ?? throw new ArgumentException("Invalid Data");
            B = new LightBulb(d.HostName, d.MacAddress, d.Service, (uint)d.Port);
            targetSector = d.SectorMapping - 1;
        }

        public async void StartStream() {
            var c = LifxSender.getClient();
            var col = new Color {R = 0x00, G = 0x00, B = 0x00};
            await c.SetLightPowerAsync(B, TimeSpan.Zero, true).ConfigureAwait(false);
            await c.SetColorAsync(B, col, 2700).ConfigureAwait(false);
        }
        
        public async void SetColor(List<System.Drawing.Color> inputs) {
            var c = LifxSender.getClient();
            if (inputs == null || c == null) throw new ArgumentException("Invalid color inputs.");
            if (inputs.Count < 12) throw new ArgumentOutOfRangeException(nameof(inputs));
            var input = inputs[targetSector];
            if (Data.MaxBrightness < 100) {
                var col2 = ColorUtil.ClampBrightness(input, Data.MaxBrightness);
                input = System.Drawing.Color.FromName("#" + col2.ToHex());
            }
            var nC = new Color {R = input.R, G = input.G, B = input.B};
            await c.SetColorAsync(B, nC, 7500).ConfigureAwait(false);
        }

        public async void StopStream() {
            var c = LifxSender.getClient();
            if (c == null) throw new ArgumentException("Invalid lifx client.");
            LogUtil.Write("Setting color back the way it was.");
            var prevColor = ColorUtil.HslToColor(Data.Hue, Data.Saturation, Data.Brightness);
            var nC = new Color {R = prevColor.R, G = prevColor.G, B = prevColor.B};
            await c.SetColorAsync(B, nC, (ushort) Data.Kelvin).ConfigureAwait(false);
            await c.SetLightPowerAsync(B, TimeSpan.Zero, Data.Power).ConfigureAwait(false);

            
        }
    }
}