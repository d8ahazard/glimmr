using System;
using System.Collections.Generic;
using System.Threading;
using HueDream.Controllers;
using HueDream.Models.Util;
using LifxNet;
using Newtonsoft.Json;

namespace HueDream.Models.StreamingDevice.LIFX {
    public class LifxBulb : IStreamingDevice {
        private LifxData Data { get; set; }
        private LightBulb B { get; }
        public bool Streaming { get; set; }

        private int _targetSector;
        
        private int _captureMode;
        public int Brightness { get; set; }
        public string Id { get; set; }
        public string IpAddress { get; set; }

        private LifxClient _client;
        
        public LifxBulb(LifxData d, LifxClient c) {
            _captureMode = DataUtil.GetItem<int>("captureMode");
            Data = d ?? throw new ArgumentException("Invalid Data");
            _client = c;
            B = new LightBulb(d.HostName, d.MacAddress, d.Service, (uint)d.Port);
            _targetSector = d.TargetSector - 1;
            Brightness = d.Brightness;
            Id = d.Id;
            IpAddress = d.IpAddress;
        }

        public async void StartStream(CancellationToken ct) {
            LogUtil.Write("Lifx: Starting stream.");
            var col = new Color {R = 0x00, G = 0x00, B = 0x00};
            Streaming = true;
            await _client.SetLightPowerAsync(B, TimeSpan.Zero, true).ConfigureAwait(false);
            LogUtil.Write("Power set.");
            await _client.SetColorAsync(B, col, 2700).ConfigureAwait(false);
            LogUtil.Write("Lifx: Streaming is active.");
            while (!ct.IsCancellationRequested) {
                Streaming = true;
            }

            StopStream();
            LogUtil.Write("Lifx: Stream stopped.");
        }

        
        public void StopStream() {
            Streaming = false;
            if (_client == null) throw new ArgumentException("Invalid lifx client.");
            LogUtil.Write("Setting color back the way it was.");
            _client.SetColorAsync(B, Data.Hue, Data.Saturation,Convert.ToUInt16(Data.Brightness), Data.Kelvin, TimeSpan.Zero);
            _client.SetLightPowerAsync(B, TimeSpan.Zero, Data.Power).ConfigureAwait(false);
        }

        public void ReloadData() {
            var newData = DataUtil.GetCollectionItem<LifxData>("lifxBulbs", Id);
            _captureMode = DataUtil.GetItem<int>("captureMode");
            Data = newData;
            var targetSector = _captureMode == 0 ? newData.TargetSector : newData.TargetSectorV2;
            _targetSector = targetSector - 1;
            Brightness = newData.MaxBrightness;
            Id = newData.Id;
        }

        public void SetColor(List<System.Drawing.Color> inputs, double fadeTime = 0) {
            if (!Streaming) return;
            if (inputs == null || _client == null) throw new ArgumentException("Invalid color inputs.");
            var capCount = _captureMode == 0 ? 12 : 28;
            if (inputs.Count < capCount) throw new ArgumentOutOfRangeException(nameof(inputs));
            var input = inputs[_targetSector];
            if (Brightness < 100) {
                input = ColorTransformUtil.ClampBrightness(input, Brightness);
            }
            var nC = new Color {R = input.R, G = input.G, B = input.B};
            var fadeSpan = TimeSpan.FromSeconds(fadeTime);
            _client.SetColorAsync(B, nC, 7500, fadeSpan);
        }

        
    }
}