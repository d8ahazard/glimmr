using System;
using System.Collections.Generic;
using System.Threading;
using Glimmr.Models.StreamingDevice.Yeelight;
using Glimmr.Models.Util;
using Glimmr.Services;
using LifxNet;
using Serilog;

namespace Glimmr.Models.StreamingDevice.LIFX {
    public class LifxDevice : IStreamingDevice {
        public bool Enable { get; set; }
        StreamingData IStreamingDevice.Data {
            get => Data;
            set => Data = (LifxData) value;
        }

        public LifxData Data { get; set; }
        private LightBulb B { get; }
        public bool Streaming { get; set; }
        public bool Testing { get; set; }

        private int _targetSector;
        
        private int _captureMode;
        public int Brightness { get; set; }
        public string Id { get; set; }
        public string IpAddress { get; set; }
        public string Tag { get; set; }

        private LifxClient _client;

        public LifxDevice(LifxData d, LifxClient c, ColorService colorService) {
            _captureMode = DataUtil.GetItem<int>("captureMode");
            Data = d ?? throw new ArgumentException("Invalid Data");
            _client = c;
            colorService.ColorSendEvent += SetColor;
            B = new LightBulb(d.HostName, d.MacAddress, d.Service, (uint)d.Port);
            _targetSector = d.TargetSector - 1;
            Brightness = d.Brightness;
            Id = d.Id;
            IpAddress = d.IpAddress;
        }

        public async void StartStream(CancellationToken ct) {
            if (!Data.Enable) return;
            Log.Debug("Lifx: Starting stream.");
            var col = new Color {R = 0x00, G = 0x00, B = 0x00};
            Streaming = true;
            await _client.SetLightPowerAsync(B, TimeSpan.Zero, true).ConfigureAwait(false);
            await _client.SetColorAsync(B, col, 2700).ConfigureAwait(false);
            Log.Debug("Lifx: Streaming is active...");
            while (!ct.IsCancellationRequested) {
                Streaming = true;
            }

            StopStream();
        }

        public void FlashColor(System.Drawing.Color color) {
            var nC = new Color {R = color.R, G = color.G, B = color.B};
            var fadeSpan = TimeSpan.FromSeconds(0);
            _client.SetColorAsync(B, nC, 7500, fadeSpan);
        }

        public bool IsEnabled() {
            return Data.Enable;
        }

        
        public void StopStream() {
            Streaming = false;
            if (_client == null) throw new ArgumentException("Invalid lifx client.");
            Log.Debug("Setting color back the way it was.");
            _client.SetColorAsync(B, Data.Hue, Data.Saturation,Convert.ToUInt16(Data.Brightness), Data.Kelvin, TimeSpan.Zero);
            _client.SetLightPowerAsync(B, TimeSpan.Zero, Data.Power).ConfigureAwait(false);
            Log.Debug("Lifx: Stream stopped.");
        }

        public void ReloadData() {
            var newData = DataUtil.GetCollectionItem<LifxData>("Dev_Lifx", Id);
            _captureMode = DataUtil.GetItem<int>("captureMode");
            Data = newData;
            var targetSector = _captureMode == 0 ? newData.TargetSector : newData.TargetSectorV2;
            _targetSector = targetSector - 1;
            Brightness = newData.MaxBrightness;
            Id = newData.Id;
        }

        public void Dispose() {
            
        }

        public void SetColor(List<System.Drawing.Color> _, List<System.Drawing.Color> sectors, double fadeTime = 0) {
            if (!Streaming || !Enable || Testing) return;
            if (sectors == null || _client == null) {
                return;
            }
            var input = sectors[_targetSector];
            if (Brightness < 100) {
                input = ColorTransformUtil.ClampBrightness(input, Brightness);
            }
            var nC = new Color {R = input.R, G = input.G, B = input.B};
            var fadeSpan = TimeSpan.FromSeconds(fadeTime);
            _client.SetColorAsync(B, nC, 7500, fadeSpan);
        }

        
    }
}