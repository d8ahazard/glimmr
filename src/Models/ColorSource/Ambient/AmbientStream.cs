using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
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
        private EasingMode _easingMode;
        private string[] _colors;
        private AnimationMode _mode;
        private int _colorIndex;
        private int _ledCount;
        private int _ambientShow;
        private string _ambientColor;
        private readonly ColorService _cs;
        private readonly Stopwatch _watch;
        private JsonLoader _loader;
        private List<AmbientScene> _scenes;
        private int _sectorCount;
        private readonly Random _random;
        private List<Color> _currentColors;
        private List<Color> _nextColors;

        
        /// <summary>
        /// Linear - Each color from the list of colors is assigned to a sector, and the order is incremented by 1 each update
        /// Reverse - Same as linear, but the order is decremented each update
        /// Random - A random color will be assigned to each sector every update
        /// RandomAll - One random color will be selected and applied to all sectors each update
        /// LinearAll - One color will be selected and applied to all tiles, with the color incremented each update
        /// </summary>
        private enum AnimationMode {
            Linear = 0,
            Reverse = 1,
            Random = 2,
            RandomAll = 3,
            LinearAll = 4
        }

        private enum EasingMode {
            Blend = 0,
            FadeIn = 1,
            FadeOut = 2,
            FadeInOut = 3
        }
        
        public AmbientStream(ColorService colorService) {
            _cs = colorService;
            _cs.AddStream("ambient", this);
            _watch = new Stopwatch();
            _random = new Random();
            Refresh();
        }

        protected override Task ExecuteAsync(CancellationToken ct) {
            return Task.Run(async () => {
                // Load this one for fading
                while (!ct.IsCancellationRequested) {
                    if (!_enable) continue;
                    var diff = _animationTime  - _watch.ElapsedMilliseconds;
                    var sectors = new List<Color>();
                    
                    if (diff > 0 && diff <= _easingTime) {
                        var avg = diff / _easingTime;
                        for (var i = 0; i < _currentColors.Count; i++) {
                            switch (_easingMode) {
                                case EasingMode.Blend:
                                    sectors.Add(BlendColor(_currentColors[i], _nextColors[i], avg));
                                    break;
                                case EasingMode.FadeIn:
                                    sectors.Add(FadeIn(_currentColors[i], avg));
                                    break;
                                case EasingMode.FadeOut:
                                    sectors.Add(FadeOut(_currentColors[i], avg));
                                    break;
                                case EasingMode.FadeInOut:
                                    sectors.Add(FadeInOut(_nextColors[i], avg));
                                    break;
                            }
                        }
                    } else if (diff <= 0) {
                        switch (_easingMode) {
                            case EasingMode.Blend:
                            case EasingMode.FadeOut:
                                _currentColors = _nextColors;
                                _nextColors = RefreshColors(_colors);
                                sectors = _currentColors;
                                break;
                            case EasingMode.FadeIn:
                            case EasingMode.FadeInOut:
                                _currentColors = _nextColors;
                                _nextColors = RefreshColors(_colors);
                                sectors = ColorUtil.EmptyList(_currentColors.Count);
                                break;
                        }
                        
                        _watch.Restart();
                    } else {
                        sectors = _currentColors;
                    }

                    var leds = SplitColors(sectors);
                    Colors = leds;
                    Sectors = sectors;
                    Log.Debug("Sending colors...");
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

        
        private static Color BlendColor(Color target, Color dest, double percent) {
            var r1 =(int) ((target.R - dest.R) * percent) + dest.R;
            var g1 =(int) ((target.G - dest.G) * percent) + dest.G;
            var b1 =(int) ((target.B - dest.B) * percent) + dest.B;
            r1 = r1 > 255 ? 255 : r1 < 0 ? 0 : r1;
            g1 = g1 > 255 ? 255 : g1 < 0 ? 0 : g1;
            b1 = b1 > 255 ? 255 : b1 < 0 ? 0 : b1;
            return Color.FromArgb(255, r1, g1, b1);
        }

        private static Color FadeOut(Color target, double percent) {
            return BlendColor(target, Color.FromArgb(255, 0, 0, 0), percent);
        }

        private static Color FadeIn(Color target, double percent) {
            return BlendColor(Color.FromArgb(255, 0, 0, 0), target, percent);
        }

        private static Color FadeInOut(Color target, double percent) {
            if (percent <= .5) {
                return FadeOut(target, percent * 2);
            }
            var pct = (percent - .5) * 2;
            return FadeIn(target, pct);
        }

        public void ToggleStream(bool enable = false) {
            _enable = enable;
        }


        private List<Color> RefreshColors(string[] input) {
            var output = new List<Color>();
            if (input == null) return ColorUtil.EmptyList(5);
            var max = input.Length;
            var rand = _random.Next(0, max);
            switch (_mode) {
                case AnimationMode.Linear:
                    for (var i = 0; i < _sectorCount; i++) {
                        output.Add(ColorTranslator.FromHtml(input[_colorIndex]));
                        _colorIndex = CycleInt(_colorIndex, max);
                    }
                    _colorIndex = CycleInt(_colorIndex, max);
                    break;
                case AnimationMode.Reverse:
                    for (var i = 0; i < _sectorCount; i++) {
                        output.Add(ColorTranslator.FromHtml(input[_colorIndex]));
                        _colorIndex = CycleInt(_colorIndex, max, true);
                    }
                    break;
                case AnimationMode.Random:
                    for (var i = 0; i < _sectorCount; i++) {
                        output.Add(ColorTranslator.FromHtml(input[rand]));
                        rand = _random.Next(0, max);
                    }
                    break;
                case AnimationMode.RandomAll:
                    for (var i = 0; i < _sectorCount; i++) {
                        output.Add(ColorTranslator.FromHtml(input[rand]));
                    }
                    break;
                case AnimationMode.LinearAll:
                    for (var i = 0; i < _sectorCount; i++) {
                        output.Add(ColorTranslator.FromHtml(input[_colorIndex]));
                    }
                    _colorIndex = CycleInt(_colorIndex, max);
                    break;
                default:
                    Log.Debug("Unknown animation mode: " + _mode);
                    break;
            }

            return output;
        }

        private static int CycleInt(int input, int max, bool reverse = false) {
            if (reverse) {
                input--;
            } else {
                input++;
            }

            if (input >= max) input = 0;
            if (input < 0) input = max;
            return input;
        }

        private void LoadScene() {
            _colorIndex = 0;
            _watch.Restart();
            // Load two arrays of colors, which we will use for the actual fade values
            _currentColors = RefreshColors(_colors);
            _nextColors = RefreshColors(_colors);
        }

        
        public void Refresh() {
            var sd = DataUtil.GetSystemData();
            _sectorCount = (sd.VSectors + sd.HSectors) * 2 - 4;
            _ledCount = sd.LedCount;
            _ambientShow = sd.AmbientShow;
            _ambientColor = sd.AmbientColor;
            _loader = new JsonLoader("ambientScenes");
            _scenes = _loader.LoadFiles<AmbientScene>();
            AmbientScene scene = new AmbientScene();
            foreach (var s in _scenes.Where(s => s.Id == _ambientShow)) {
                scene = s;
            }

            if (_ambientShow == -1) scene.Colors = new[] {"#" + _ambientColor};
            _colors = scene.Colors;
            _animationTime = scene.AnimationTime * 1000;
            _easingTime = scene.EasingTime * 1000;
            _easingMode = (EasingMode) scene.Easing;
            _mode = (AnimationMode) scene.Mode;
            LoadScene();
        }

        public List<Color> Colors { get; private set; }
        public List<Color> Sectors { get; private set; }
        
    }
}