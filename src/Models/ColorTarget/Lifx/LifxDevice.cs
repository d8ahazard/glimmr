using System;
using System.Collections.Generic;
using System.Globalization;
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
        private bool _hasMulti;
        private int _multizoneCount;
        private int _offset;
        private bool _reverseStrip;
        public int Brightness { get; set; }
        public string Id { get; set; }
        public string IpAddress { get; set; }
        public string Tag { get; set; }

        private readonly LifxClient _client;
        
        
        public LifxDevice(LifxData d, ColorService colorService) : base(colorService) {
            DataUtil.GetItem<int>("captureMode");
            Data = d ?? throw new ArgumentException("Invalid Data");
            _hasMulti = d.HasMultiZone;
            _offset = d.Offset;
            _reverseStrip = d.ReverseStrip;
            if (_hasMulti) _multizoneCount = d.LedCount;
            _client = colorService.ControlService.GetAgent("LifxAgent");
            colorService.ColorSendEvent += SetColor;
            B = new LightBulb(d.HostName, d.MacAddress, d.Service, (uint)d.Port);
            _targetSector = Data.TargetSector - 1;
            _targetSector = ColorUtil.CheckDsSectors(_targetSector);
            Brightness = d.Brightness;
            Id = d.Id;
            IpAddress = d.IpAddress;
            Enable = Data.Enable;
        }

       
        public async Task StartStream(CancellationToken ct) {
            if (!Enable) return;
            Log.Information($"{Data.Tag}::Starting stream: {Data.Id}...");
            // Recalculate target sector before starting stream, just in case.
            _targetSector = Data.TargetSector - 1;
            _targetSector = ColorUtil.CheckDsSectors(_targetSector);
            var col = new LifxColor(0, 0, 0);
            //var col = new LifxColor {R = 0, B = 0, G = 0};
            _client.SetLightPowerAsync(B, true);
            _client.SetColorAsync(B, col, 2700);
            Streaming = true;
            await Task.FromResult(Streaming);
            Log.Information($"{Data.Tag}::Stream started: {Data.Id}.");
        }

        public async Task FlashColor(Color color) {
            var nC = new LifxColor(color);
            //var nC = new LifxColor {R = color.R, B = color.B, G = color.G};
            await _client.SetColorAsync(B, nC).ConfigureAwait(false);
        }
        
        

        public bool IsEnabled() {
            return Enable;
        }

        
        public async Task StopStream() {
            if (!Enable) return;
            Streaming = false;
            if (_client == null) return;
            FlashColor(Color.FromArgb(0, 0, 0)).ConfigureAwait(false);
            _client.SetLightPowerAsync(B, Data.Power).ConfigureAwait(false);
            await Task.FromResult(true);
            Log.Information($"{Data.Tag}::Stream stopped: {Data.Id}.");
        }

        public Task ReloadData() {
            var newData = DataUtil.GetDevice<LifxData>(Id);
            DataUtil.GetItem<int>("captureMode");
            Data = newData;
            _hasMulti = Data.HasMultiZone;
            _offset = Data.Offset;
            _reverseStrip = Data.ReverseStrip;
            if (_hasMulti) _multizoneCount = Data.LedCount;

            IpAddress = Data.IpAddress;
            var targetSector = newData.TargetSector;
            _targetSector = targetSector - 1;
            var oldBrightness = Brightness;
            Brightness = newData.Brightness;
            if (oldBrightness != Brightness) {
                var bri = Brightness / 100 * 255;
                _client.SetBrightnessAsync(B, (ushort) bri).ConfigureAwait(false);
            }
            Id = newData.Id;
            Enable = Data.Enable;
            return Task.CompletedTask;
        }

        public void Dispose() {
            
        }

        public void SetColor(List<Color> colors, List<Color> list, int arg3, bool force=false) {
            if (!Streaming || !Enable || Testing && !force) return;
            if (_hasMulti) {
                SetColorMulti(colors);
            } else {
                SetColorSingle(list);
            }
            ColorService.Counter.Tick(Id);
        }

        private void SetColorMulti(List<Color> colors) {
            if (colors == null || _client == null) {
                Log.Warning("Null client or no colors!");
                return;
            }
            var shifted = new List<Color>();

            var output = ColorUtil.TruncateColors(colors, _offset, _multizoneCount);
            
            if (_reverseStrip) output.Reverse();
            var i = 0;
            shifted = new List<Color>();

            foreach (var col in output) {
                if (i == 0) {
                    shifted.Add(ColorUtil.FixGamma(col));
                    i = 1;
                } else {
                    i = 0;
                }
            }

            var cols = new List<LifxColor>();
            foreach (var c in shifted) {
                if (Brightness < 100) {
                    var bFactor = Brightness / 100f;
                    ColorUtil.ColorToHsv(c, out var h, out var s, out var v);
                    v *= bFactor;
                    s = scaleSaturation(s);
                    cols.Add(new LifxColor(h,s,v,3500d));
                } else {
                    cols.Add(new LifxColor(c));
                }
            }
            _client.SetExtendedColorZonesAsync(B, cols).ConfigureAwait(false);
        }

        private double scaleSaturation(double input) {
            if (input >= 1d) return input;
            var diff = 1d - input;
            diff *= 3;
            input -= diff;
            return Math.Max(input, 0d);
        }

        private void SetColorSingle(List<Color> list) {
            
            var sectors = list;
            if (sectors == null || _client == null) {
                return;
            }

            if (_targetSector >= sectors.Count) return;
            
            var input = sectors[_targetSector];
            
            var nC = new LifxColor(input);
            //var nC = new LifxColor {R = input.R, B = input.B, G = input.G};

            _client.SetColorAsync(B, nC).ConfigureAwait(false);
            ColorService.Counter.Tick(Id);
        }
        
    }
}