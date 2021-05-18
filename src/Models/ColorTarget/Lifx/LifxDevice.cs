using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models.Util;
using Glimmr.Services;
using LifxNetPlus;
using Serilog;
using Color = System.Drawing.Color;

namespace Glimmr.Models.ColorTarget.Lifx {
    public class LifxDevice : ColorTarget, IColorTarget {
        public bool Enable { get; set; }
        public bool Online { get; set; }

        IColorTargetData IColorTarget.Data {
            get => Data;
            set => Data = (LifxData) value;
        }

        public LifxData Data { get; set; }
        private LightBulb B { get; }
        public bool Streaming { get; set; }
        public bool Testing { get; set; }

        private int _target;
        private bool _hasMulti;
        private int _multizoneCount;
        private int _offset;
        private bool _reverseStrip;
        public int Brightness { get; set; }
        public string Id { get; set; }
        public string IpAddress { get; set; }
        public string Tag { get; set; }

        private readonly LifxClient _client;
        private TimeSpan _frameSpan;

        private CaptureMode _capMode;
        private int _sectorCount;
        
        public LifxDevice(LifxData d, ColorService colorService) : base(colorService) {
            RefreshSystem();
            _frameSpan = TimeSpan.FromMilliseconds(1000f/60);
            DataUtil.GetItem<int>("captureMode");
            Data = d ?? throw new ArgumentException("Invalid Data");
            _hasMulti = d.HasMultiZone;
            _offset = d.Offset;
            _reverseStrip = d.ReverseStrip;
            if (_hasMulti) _multizoneCount = d.LedCount;
            _client = colorService.ControlService.GetAgent("LifxAgent");
            colorService.ColorSendEvent += SetColor;
            colorService.ControlService.RefreshSystemEvent += RefreshSystem;
            B = new LightBulb(d.HostName, d.MacAddress, d.Service, (uint)d.Port);
            _target = d.TargetSector - 1;
            if (_capMode == CaptureMode.DreamScreen && _target > -1) {
                var tPct = _target / _sectorCount;
                _target = tPct * 12;
                _target = Math.Min(_target, 11);
            }
            Brightness = d.Brightness;
            Id = d.Id;
            IpAddress = d.IpAddress;
            Enable = Data.Enable;
            Online = SystemUtil.IsOnline(IpAddress);
        }

        private void RefreshSystem() {
            var sd = DataUtil.GetSystemData();
            _capMode = (CaptureMode) sd.CaptureMode;
            _sectorCount = sd.SectorCount;
        }

        public async Task StartStream(CancellationToken ct) {
            if (!Enable) return;
            Log.Debug("Lifx: Starting stream.");
            var col = new LifxColor(0, 0, 0);
            //var col = new LifxColor {R = 0, B = 0, G = 0};
            _client.SetLightPowerAsync(B, true);
            _client.SetColorAsync(B, col, 2700);
            Log.Debug($"Lifx: Streaming is active, {_hasMulti} {_multizoneCount}");
            Streaming = true;
            await Task.FromResult(Streaming);
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
            if (!Enable || !Online) return;
            Streaming = false;
            if (_client == null) throw new ArgumentException("Invalid lifx client.");
            FlashColor(Color.FromArgb(0, 0, 0)).ConfigureAwait(false);
            _client.SetLightPowerAsync(B, Data.Power).ConfigureAwait(false);
            await Task.FromResult(true);
            Log.Debug("Lifx: Stream stopped.");
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
            _target = targetSector - 1;
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
            if (Brightness < 100) {
                var diff = Brightness / 100f;
                foreach (var rgb in output) {
                    var r = Math.Clamp(rgb.R * diff, 0, 255);
                    var g = Math.Clamp(rgb.G * diff, 0, 255);
                    var b = Math.Clamp(rgb.B * diff, 0, 255);
                    shifted.Add(Color.FromArgb((int)r, (int)g, (int)b));
                }
                output = shifted;
            }
            if (_reverseStrip) output.Reverse();
            var i = 0;
            shifted = new List<Color>();

            foreach (var col in output) {
                if (i == 0) {
                    shifted.Add(col);
                    i = 1;
                } else {
                    i = 0;
                }
            }

            output = shifted;
            var cols = output.Select(col => new LifxColor(col)).ToList();
            
            _client.SetExtendedColorZonesAsync(B, cols).ConfigureAwait(false);
        }

        private void SetColorSingle(List<Color> list) {
            
            var sectors = list;
            if (sectors == null || _client == null) {
                return;
            }

            if (_target >= sectors.Count) return;
            
            var input = sectors[_target];
            
            var nC = new LifxColor(input);
            //var nC = new LifxColor {R = input.R, B = input.B, G = input.G};

            _client.SetColorAsync(B, nC).ConfigureAwait(false);
            ColorService.Counter.Tick(Id);
        }
        
    }
}