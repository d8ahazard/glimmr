using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using LifxNet;
using Serilog;
using Color = System.Drawing.Color;
//using LifxColor = LifxNet.Color;

namespace Glimmr.Models.ColorTarget.LIFX {
    public class LifxDevice : IColorTarget {
        public bool Enable { get; set; }
        StreamingData IColorTarget.Data {
            get => Data;
            set => Data = (LifxData) value;
        }

        public LifxData Data { get; set; }
        private LightBulb B { get; }
        public bool Streaming { get; set; }
        public bool Testing { get; set; }

        private int _targetSector;
        public int Brightness { get; set; }
        public string Id { get; set; }
        public string IpAddress { get; set; }
        public string Tag { get; set; }

        private readonly LifxClient _client;

        public LifxDevice(LifxData d, LifxClient c, ColorService colorService) {
            DataUtil.GetItem<int>("captureMode");
            Data = d ?? throw new ArgumentException("Invalid Data");
            _client = c;
            colorService.ColorSendEvent += SetColor;
            B = new LightBulb(d.HostName, d.MacAddress, d.Service, (uint)d.Port);
            _targetSector = d.TargetSector - 1;
            Brightness = d.Brightness;
            Id = d.Id;
            IpAddress = d.IpAddress;
        }

        public async Task StartStream(CancellationToken ct) {
            if (!Data.Enable) return;
            Log.Debug("Lifx: Starting stream.");
            var col = new LifxColor(0, 0, 0);
            //var col = new LifxColor {R = 0, B = 0, G = 0};
            Streaming = true;
            await _client.SetLightPowerAsync(B, TimeSpan.Zero, true).ConfigureAwait(false);
            await _client.SetColorAsync(B, col, 2700).ConfigureAwait(false);
            Log.Debug("Lifx: Streaming is active...");
            Streaming = true;
        }

        public async Task FlashColor(Color color) {
            var nC = new LifxColor(color);
            //var nC = new LifxColor {R = color.R, B = color.B, G = color.G};
            var fadeSpan = TimeSpan.FromSeconds(0);
            await _client.SetColorAsync(B, nC, 7500, fadeSpan);
        }

        public bool IsEnabled() {
            return Data.Enable;
        }

        
        public async Task StopStream() {
            Streaming = false;
            if (_client == null) throw new ArgumentException("Invalid lifx client.");
            Log.Debug("Setting color back the way it was.");
            await _client.SetColorAsync(B, Data.Hue, Data.Saturation,Convert.ToUInt16(Data.Brightness), Data.Kelvin, TimeSpan.Zero);
            await _client.SetLightPowerAsync(B, TimeSpan.Zero, Data.Power).ConfigureAwait(false);
            Log.Debug("Lifx: Stream stopped.");
        }

        public Task ReloadData() {
            var newData = DataUtil.GetCollectionItem<LifxData>("Dev_Lifx", Id);
            DataUtil.GetItem<int>("captureMode");
            Data = newData;
            var targetSector = newData.TargetSector;
            _targetSector = targetSector - 1;
            Brightness = newData.MaxBrightness;
            Id = newData.Id;
            return Task.CompletedTask;
        }

        public void Dispose() {
            
        }

        public void SetColor(List<Color> colors, List<Color> list, int arg3) {
            if (!Streaming || !Enable || Testing) return;
            var sectors = list;
            var fadeTime = arg3;
            if (sectors == null || _client == null) {
                return;
            }

            if (_targetSector >= sectors.Count) return;
            var input = sectors[_targetSector];
            if (Brightness < 100) {
                //input = ColorUtil.ClampBrightness(input, Brightness);
            }
            var nC = new LifxColor(input);
            //var nC = new LifxColor {R = input.R, B = input.B, G = input.G};

            var fadeSpan = TimeSpan.FromSeconds(fadeTime);
            _client.SetColorAsync(B, nC, 7500, fadeSpan);
        }

        
    }
}