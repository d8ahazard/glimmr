using System;
using System.Collections.Generic;
using System.Threading;
using HueDream.Models.Util;
using LifxNet;

namespace HueDream.Models.StreamingDevice.LIFX {
    public class LifxBulb : IStreamingDevice {
        private LifxData Data { get; }
        private LightBulb B { get; }
        public bool Streaming { get; set; }

        private readonly int targetSector;
        public LifxBulb(LifxData d) {
            Data = d ?? throw new ArgumentException("Invalid Data");
            B = new LightBulb(d.HostName, d.MacAddress, d.Service, (uint)d.Port);
            targetSector = d.SectorMapping - 1;
        }

        public async void StartStream(CancellationToken ct) {
            LogUtil.Write("Lifx: Starting stream.");
            var c = LifxSender.getClient();
            var col = new Color {R = 0x00, G = 0x00, B = 0x00};
            Streaming = true;
            await c.SetLightPowerAsync(B, TimeSpan.Zero, true).ConfigureAwait(false);
            await c.SetColorAsync(B, col, 2700).ConfigureAwait(false);
            while (!ct.IsCancellationRequested) {
                Streaming = true;
            }

            StopStream();
            LogUtil.Write("Lifx: Stream stopped.");
        }

        
        public void StopStream() {
            Streaming = false;
            var c = LifxSender.getClient();
            if (c == null) throw new ArgumentException("Invalid lifx client.");
            LogUtil.Write("Setting color back the way it was.");
            var prevColor = ColorUtil.HslToColor(Data.Hue, Data.Saturation, Data.Brightness);
            var nC = new Color {R = prevColor.R, G = prevColor.G, B = prevColor.B};
            c.SetColorAsync(B, nC, (ushort) Data.Kelvin).ConfigureAwait(false);
            c.SetLightPowerAsync(B, TimeSpan.Zero, Data.Power).ConfigureAwait(false);
        }
        
        public void SetColor(List<System.Drawing.Color> inputs, double fadeTime = 0) {
            if (!Streaming) {
                LogUtil.Write("Lifx: We are not streaming, returning.");
                return;
            }
            var c = LifxSender.getClient();
            if (inputs == null || c == null) throw new ArgumentException("Invalid color inputs.");
            if (inputs.Count < 12) throw new ArgumentOutOfRangeException(nameof(inputs));
            var input = inputs[targetSector];
            if (Data.MaxBrightness < 100) {
                input = ColorUtil.ClampBrightness(input, Data.MaxBrightness);
            }
            var nC = new Color {R = input.R, G = input.G, B = input.B};
            var fadeSpan = TimeSpan.FromSeconds(fadeTime);
            c.SetColorAsync(B, nC, 7500, fadeSpan);
        }

        
    }
}