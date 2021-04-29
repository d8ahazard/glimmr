using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using LifxNetPlus;
using Serilog;
using Color = System.Drawing.Color;

namespace Glimmr.Models.ColorTarget.Lifx {
    public class LifxDevice : ColorTarget, IColorTarget {
        public bool Enable { get; set; }
        IColorTargetData IColorTarget.Data {
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
        
        private List<List<Color>> _frameBuffer;
        private int _frameDelay;

        public LifxDevice(LifxData d, ColorService colorService) : base(colorService) {
            DataUtil.GetItem<int>("captureMode");
            Data = d ?? throw new ArgumentException("Invalid Data");
            _client = colorService.ControlService.GetAgent("LifxAgent");
            colorService.ColorSendEvent += SetColor;
            B = new LightBulb(d.HostName, d.MacAddress, d.Service, (uint)d.Port);
            _targetSector = d.TargetSector - 1;
            Brightness = d.Brightness;
            Id = d.Id;
            IpAddress = d.IpAddress;
            Enable = Data.Enable;
        }

        public async Task StartStream(CancellationToken ct) {
            if (!Enable) return;
            Log.Debug("Lifx: Starting stream.");
            var col = new LifxColor(0, 0, 0);
            //var col = new LifxColor {R = 0, B = 0, G = 0};
            _frameBuffer = new List<List<Color>>();
            Streaming = true;
            await _client.SetLightPowerAsync(B, true).ConfigureAwait(false);
            await _client.SetColorAsync(B, col, 2700).ConfigureAwait(false);
            Log.Debug("Lifx: Streaming is active...");
            Streaming = true;
        }

        public async Task FlashColor(Color color) {
            var nC = new LifxColor(color);
            //var nC = new LifxColor {R = color.R, B = color.B, G = color.G};
            await _client.SetColorAsync(B, nC);
        }

        public bool IsEnabled() {
            return Enable;
        }

        
        public async Task StopStream() {
            if (!Enable) return;
            Streaming = false;
            if (_client == null) throw new ArgumentException("Invalid lifx client.");
            await FlashColor(Color.FromArgb(0, 0, 0));
            await _client.SetLightPowerAsync(B, Data.Power).ConfigureAwait(false);
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
            Enable = Data.Enable;
            _frameDelay = Data.FrameDelay;
            _frameBuffer = new List<List<Color>>();
            return Task.CompletedTask;
        }

        public void Dispose() {
            
        }

        public void SetColor(List<Color> colors, List<Color> list, int arg3, bool force=false) {
            if (!Streaming || !Enable || Testing && !force) return;
            var sectors = list;
            if (sectors == null || _client == null) {
                return;
            }

            if (_targetSector >= sectors.Count) return;
            
            if (_frameDelay > 0) {
                _frameBuffer.Add(sectors);
                if (_frameBuffer.Count < _frameDelay) return; // Just buffer till we reach our count
                sectors = _frameBuffer[0];
                _frameBuffer.RemoveAt(0);	
            }
            
            var input = sectors[_targetSector];
            if (Brightness < 100) {
                //input = ColorUtil.ClampBrightness(input, Brightness);
            }
            var nC = new LifxColor(input);
            //var nC = new LifxColor {R = input.R, B = input.B, G = input.G};

            _client.SetColorAsync(B, nC);
            ColorService.Counter.Tick(Id);
        }

        
    }
}