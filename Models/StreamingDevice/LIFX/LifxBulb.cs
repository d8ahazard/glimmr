using System;
using System.Collections.Generic;
using System.Threading;
using HueDream.Models.Util;
using LifxNet;

namespace HueDream.Models.StreamingDevice.LIFX {
    public class LifxBulb : IStreamingDevice {
        private LifxData Data { get; set; }
        private LightBulb B { get; }
        public bool Streaming { get; set; }

        private int targetSector;
        public int MaxBrightness { get; set; }
        public string Id { get; set; }
        public LifxBulb(LifxData d) {
            Data = d ?? throw new ArgumentException("Invalid Data");
            B = new LightBulb(d.HostName, d.MacAddress, d.Service, (uint)d.Port);
            targetSector = d.SectorMapping - 1;
            MaxBrightness = d.MaxBrightness;
            Id = d.Id;
        }

        public async void StartStream(CancellationToken ct) {
            LogUtil.Write("Lifx: Starting stream.");
            var c = LifxSender.GetClient();
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
            var c = LifxSender.GetClient();
            if (c == null) throw new ArgumentException("Invalid lifx client.");
            LogUtil.Write("Setting color back the way it was.");
            c.SetColorAsync(B, Data.Hue, Data.Saturation, Data.Brightness, Data.Kelvin, TimeSpan.Zero);
            c.SetLightPowerAsync(B, TimeSpan.Zero, Data.Power).ConfigureAwait(false);
        }

        public void ReloadData() {
            var newData = DataUtil.GetCollectionItem<LifxData>("lifxBulbs", Id);
            Data = newData;
            targetSector = newData.SectorMapping - 1;
            MaxBrightness = newData.MaxBrightness;
            Id = newData.Id;
        }

        public void SetColor(List<System.Drawing.Color> inputs, double fadeTime = 0) {
            if (!Streaming) return;
            var c = LifxSender.GetClient();
            if (inputs == null || c == null) throw new ArgumentException("Invalid color inputs.");
            if (inputs.Count < 12) throw new ArgumentOutOfRangeException(nameof(inputs));
            var input = inputs[targetSector];
            if (MaxBrightness < 100) {
                input = ColorTransformUtil.ClampBrightness(input, MaxBrightness);
            }
            var nC = new Color {R = input.R, G = input.G, B = input.B};
            var fadeSpan = TimeSpan.FromSeconds(fadeTime);
            c.SetColorAsync(B, nC, 7500, fadeSpan);
        }

        
    }
}