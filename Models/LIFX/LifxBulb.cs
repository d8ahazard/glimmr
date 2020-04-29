using System;
using System.Collections.Generic;
using HueDream.Models.Util;
using LifxNet;

namespace HueDream.Models.LIFX {
    public class LifxBulb {
        public LifxData data;
        public LightBulb b;
        private int targetSector;
        public LifxBulb(LifxData d) {
            data = d ?? throw new ArgumentException("Invalid Data");
            b = new LightBulb(d.HostName, d.MacAddress, d.Service, (uint)d.Port);
            targetSector = d.SectorMapping - 1;
        }

        public async void StartStream(LifxClient c) {
            if (c == null) throw new ArgumentException("Invalid LIFX Client.");
            var col = new Color {R = 0x00, G = 0x00, B = 0x00};
            c.SetLightPowerAsync(b, TimeSpan.Zero, true);
            c.SetColorAsync(b, col, 2700);
        }
        
        public async void SetColor(LifxClient c, List<System.Drawing.Color> inputs) {
            if (inputs == null || c == null) throw new ArgumentException("Invalid color inputs.");
            if (inputs.Count < 12) throw new ArgumentOutOfRangeException(nameof(inputs));
            var input = inputs[targetSector];
            var nC = new Color {R = input.R, G = input.G, B = input.B};
            c.SetColorAsync(b, nC, 7500);
        }

        public async void StopStream(LifxClient c) {
            if (c == null) throw new ArgumentException("Invalid lifx client.");
            LogUtil.Write("Setting color back the way it was.");
            var prevColor = ColorUtil.HslToColor(data.Hue, data.Saturation, data.Brightness);
            var nC = new Color {R = prevColor.R, G = prevColor.G, B = prevColor.B};
            c.SetColorAsync(b, nC, (ushort) data.Kelvin);
            c.SetLightPowerAsync(b, TimeSpan.Zero, data.Power);

            
        }
    }
}