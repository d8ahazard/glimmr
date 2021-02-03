using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Glimmr.Models.ColorSource.Ambient {
    public class AmbientStream : BackgroundService {

        private bool _enable;
        private double _animationTime;
        private double _easingTime;
        private string[] _colors;
        private AnimationMode _mode;
        private int _startInt;
        private int _ledCount;
        private int _ambientShow;
        private string _ambientColor;
        private readonly ColorService _cs;
        private readonly Stopwatch _watch;
        private JsonLoader _loader;
        private List<AmbientScene> _scenes;
        private bool _reloaded;

        private enum AnimationMode {
            Linear = 0,
            Reverse = 1,
            Random = 2,
            RandomAll = 3,
            LinearAll = 4
        }
        
        public AmbientStream(ColorService colorService) {
            _cs = colorService;
            _cs.AddStream("ambient", this);
            _watch = new Stopwatch();
            Refresh();
        }

        protected override Task ExecuteAsync(CancellationToken ct) {
            return Task.Run(async () => {
                _startInt = 0;
                _watch.Restart();
                // Load two arrays of colors, which we will use for the actual fade values
                var current = RefreshColors(_colors);
                var next = RefreshColors(_colors);
                // Load this one for fading
                while (!ct.IsCancellationRequested) {
                    if (!_enable) continue;
                    var diff = _animationTime  - _watch.ElapsedMilliseconds;
                    var sectors = new List<Color>();
                    if (diff > 0 && diff <= _easingTime) {
                        var avg = diff / _easingTime;
                        for (var i = 0; i < current.Count; i++) {
                            sectors.Add(FadeColor(current[i], next[i], avg));
                        }
                    } else {
                        if (diff <= 0 || _reloaded) {
                            current = next;
                            next = RefreshColors(_colors);
                            sectors = current;
                            _watch.Restart();
                            _reloaded = false;
                        } else {
                            sectors = current;
                        }    
                    }

                    var leds = SplitColors(sectors);
                    Colors = leds;
                    Sectors = sectors;
                    _cs.SendColors(Colors, Sectors,0);
                    await Task.FromResult(true);
                }
                _watch.Stop();
                Log.Information("DreamScene: Color Builder canceled.");
            });
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

        public void ToggleStream(bool enable = false) {
            _enable = enable;
        }


        private List<Color> RefreshColors(string[] input) {
            var output = new List<Color>();
            var maxColors = input.Length - 1;
            var colorCount = _startInt;
            var col1 = _startInt;
            var allRand = new Random().Next(0, maxColors);
            

            if (_mode == AnimationMode.LinearAll) {
                for (var i = 0; i < 28; i++) {
                    output.Add(ColorTranslator.FromHtml(input[colorCount]));
                }
                _startInt++;
            } else {
                for (var i = 0; i < 28; i++) {
                    col1 = i + col1;
                    if (_mode == AnimationMode.Random) col1 = new Random().Next(0, maxColors);
                    while (col1 > maxColors) col1 -= maxColors;
                    if (_mode == AnimationMode.RandomAll) col1 = allRand;
                    output.Add(ColorTranslator.FromHtml(input[col1]));
                }
            }

            if (_mode == AnimationMode.Linear) _startInt++;

            if (_mode == AnimationMode.Reverse) _startInt--;
            if (_startInt > maxColors) _startInt = 0;

            if (_startInt < 0) _startInt = maxColors;
            //Console.WriteLine($@"Returning refreshed colors: {JsonConvert.SerializeObject(output)}.");
            return output;
        }

        
        public void Refresh() {
            SystemData sd = DataUtil.GetObject<SystemData>("SystemData");
            _ledCount = sd.LedCount;
            _ambientShow = sd.AmbientShow;
            _ambientColor = sd.AmbientColor;
            _loader = new JsonLoader("ambientScenes");
            _scenes = _loader.LoadFiles<AmbientScene>();
            AmbientScene scene = new AmbientScene();
            foreach (var s in _scenes) {
                if (s.Id == _ambientShow) {
                    scene = s;
                }
            }

            if (_ambientShow == -1) scene.Colors = new[] {"#" + _ambientColor};
            _colors = scene.Colors;
            _animationTime = scene.AnimationTime * 1000;
            _easingTime = scene.EasingTime * 1000;
            _mode = (AnimationMode) scene.Mode;
            _startInt = 0;
            _reloaded = true;
            Log.Debug($"Ambient scene: {_ambientShow}, {_ambientColor}, {_animationTime}");
        }

        public List<Color> Colors { get; private set; }
        public List<Color> Sectors { get; private set; }
        
    }
}