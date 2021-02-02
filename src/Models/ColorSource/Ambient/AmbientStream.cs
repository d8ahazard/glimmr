using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.ColorSource.Ambient.Scene;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;
using static Glimmr.Models.ColorSource.Ambient.IScene;

namespace Glimmr.Models.ColorSource.Ambient {
    public class AmbientStream : IColorSource {
        private double _animationTime;
        private string[] _colors;
        private AnimationMode _mode;
        private int _startInt;
        private int _ledCount;
        private int _ambientShow;
        private int _deviceMode;
        private Color _ambientColor;
        private readonly ColorService _cs;
        private TimeSpan _waitSpan;
        private Stopwatch _watch;
        private Task _sendTask;

        
        public AmbientStream(ColorService colorService, in CancellationToken ct) {
            _cs = colorService;
            _watch = new Stopwatch();
            Refresh();
        }

        public void StartStream(CancellationToken ct) {
            _startInt = 0;
            Streaming = true;
            _watch.Restart();
            // Load two arrays of colors, which we will use for the actual fade values
            var current = RefreshColors(_colors);
            var next = RefreshColors(_colors);
            // Load this one for fading
            while (!ct.IsCancellationRequested) {
                var diff = (_animationTime * 1000) - _watch.ElapsedMilliseconds;
                var sectors = new List<Color>();
                if (diff > 0) {
                    var avg = diff / (_animationTime * 1000);
                    for (var i = 0; i < current.Count; i++) {
                        sectors.Add(FadeColor(current[i], next[i], avg));
                    }
                } else {
                    current = next;
                    next = RefreshColors(_colors);
                    sectors = current;
                    _watch.Restart();
                }

                var leds = SplitColors(sectors);
                Colors = leds;
                Sectors = sectors;
                _cs.SendColors(Colors, Sectors,0);
            }
            _watch.Stop();
            Log.Information("DreamScene: Color Builder canceled.");
        }

        private List<Color> SplitColors(List<Color> input) {
            var output = new List<Color>();
            var tot = Math.Ceiling((float)(_ledCount / input.Count));
            foreach (var color in input) {
                output.AddRange(FillList(color, (int)tot));
            }
            return output;
        }

        private List<Color> FillList(Color color, int count) {
            var output = new List<Color>();
            for (var i = 0; i < count; i++) {
                output.Add(color);
            }
            return output;
        }

        
        private Color FadeColor(Color target, Color dest, double percent) {
            var r1 =(int) ((target.R - dest.R) * percent) + dest.R;
            var g1 =(int) ((target.G - dest.G) * percent) + dest.G;
            var b1 =(int) ((target.B - dest.B) * percent) + dest.B;
            r1 = r1 > 255 ? 255 : r1 < 0 ? 0 : r1;
            g1 = g1 > 255 ? 255 : g1 < 0 ? 0 : g1;
            b1 = b1 > 255 ? 255 : b1 < 0 ? 0 : b1;
            return Color.FromArgb(255, r1, g1, b1);
        }

        public void StopStream() {
            Streaming = false;
            //throw new NotImplementedException();
        }


        private List<Color> RefreshColors(string[] input) {
            var output = new List<Color>();
            var maxColors = input.Length - 1;
            var colorCount = _startInt;
            var col1 = _startInt;
            var allRand = new Random().Next(0, maxColors);
            

            if (_mode == AnimationMode.LinearAll) {
                for (var i = 0; i < 28; i++) {
                    output.Add(ColorTranslator.FromHtml("#" + input[colorCount]));
                }
                _startInt++;
            } else {
                for (var i = 0; i < 28; i++) {
                    col1 = i + col1;
                    if (_mode == AnimationMode.Random) col1 = new Random().Next(0, maxColors);
                    while (col1 > maxColors) col1 -= maxColors;
                    if (_mode == AnimationMode.RandomAll) col1 = allRand;
                    output.Add(ColorTranslator.FromHtml("#" + input[col1]));
                }
            }

            if (_mode == AnimationMode.Linear) _startInt++;

            if (_mode == AnimationMode.Reverse) _startInt--;
            if (_startInt > maxColors) _startInt = 0;

            if (_startInt < 0) _startInt = maxColors;
            //Console.WriteLine($@"Returning refreshed colors: {JsonConvert.SerializeObject(output)}.");
            return output;
        }

        public bool Streaming { get; set; }
        public void ToggleSend(bool enable = true) {
            Streaming = enable;
        }

        
        public void Refresh() {
            SystemData sd = DataUtil.GetObject<SystemData>("SystemData");
            _ledCount = sd.LedCount;
            _deviceMode = sd.DeviceMode;
            _ambientShow = DataUtil.GetItem<int>("AmbientShow") ?? 0;
            _ambientColor = DataUtil.GetObject<Color>("AmbientColor") ?? Color.FromArgb(255,255,255,255);
            
            IScene scene = _ambientShow switch {
                -1 => new SceneSolid(_ambientColor),
                0 => new SceneRandom(),
                1 => new SceneFire(),
                2 => new SceneTwinkle(),
                3 => new SceneOcean(),
                4 => new SceneRainbow(),
                5 => new SceneJuly(),
                6 => new SceneHoliday(),
                7 => new ScenePop(),
                8 => new SceneForest(),
                _ => null
            };

            if (scene == null) return;
            _colors = scene.GetColors();
            _animationTime = scene.AnimationTime;
            _mode = scene.Mode;
            _startInt = 0;
            _watch.Restart();
            Log.Debug($"Ambient scene: {_ambientShow}, {_ambientColor}, {_animationTime}");
        }

        public List<Color> Colors { get; private set; }
        public List<Color> Sectors { get; private set; }
    }
}