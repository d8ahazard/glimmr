using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using Glimmr.Models.ColorSource.Ambient.Scenes;
using Glimmr.Models.LED;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Serilog;
using static Glimmr.Models.ColorSource.Ambient.Scenes.SceneBase;

namespace Glimmr.Models.ColorSource.Ambient {
    public class AmbientStream : IColorSource {
        private double _animationTime;
        private string[] _colors;
        private AnimationMode _mode;
        private int _startInt;
        private int _ledCount;
        private int _ambientMode;
        private int _ambientShow;
        private Color _ambientColor;
        private CancellationToken _ct;
        private ColorService _cs;


        
        public AmbientStream(ColorService colorService, in CancellationToken ct) {
            _cs = colorService;
            _ct = ct;
            Refresh();
        }

        public void Initialize() {
            _startInt = 0;
            var startTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            Log.Debug($@"Color builder started, animation time is {_animationTime}...");
            while (!_ct.IsCancellationRequested) {
                if (!Streaming) continue;
                var curTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                var dTime = curTime - startTime;
                // Check and set colors if time is greater than animation int, then reset time count...
                if (!(dTime > _animationTime * 1000)) continue;
                startTime = curTime;
                var cols = RefreshColors(_colors);
                var ledCols = new List<Color>();
                for (var i = 0; i < _ledCount; i++) {
                    var r = i / _ledCount * cols.Count;
                    ledCols.Add(cols[r]);
                }
                _cs.SendColors(ledCols, cols);
            }

            Log.Debug($@"DreamScene: Color Builder canceled. {startTime}");
        }
        
        
        private List<Color> RefreshColors(string[] input) {
            var output = new List<Color>();
            var maxColors = input.Length - 1;
            var colorCount = _startInt;
            var col1 = _startInt;
            var allRand = new Random().Next(0, maxColors);
            for (var i = 0; i < 24; i++) {
                col1 = i + col1;
                if (_mode == AnimationMode.Random) col1 = new Random().Next(0, maxColors);
                while (col1 > maxColors) col1 -= maxColors;
                if (_mode == AnimationMode.RandomAll) col1 = allRand;
                output.Add(ColorTranslator.FromHtml("#" + input[col1]));
            }

            if (_mode == AnimationMode.LinearAll) {
                for (var i = 0; i < 12; i++) {
                    output.Add(ColorTranslator.FromHtml("#" + input[colorCount]));
                }
                _startInt++;
            }

            if (_mode == AnimationMode.Linear) _startInt++;

            if (_mode == AnimationMode.Reverse) _startInt--;
            if (_startInt > maxColors) _startInt = 0;

            if (_startInt < 0) _startInt = maxColors;
            Console.WriteLine($@"Returning refreshed colors: {JsonConvert.SerializeObject(output)}.");
            return output;
        }

        public bool Streaming { get; set; }
        public void ToggleSend(bool enable = true) {
            Streaming = enable;
        }

        
        public void Refresh() {
            LedData ld = DataUtil.GetObject<LedData>("LedData");
            _ledCount = ld.LedCount;
            _ambientMode = DataUtil.GetItem<int>("AmbientMode") ?? 0;
            _ambientShow = DataUtil.GetItem<int>("AmbientShow") ?? 0;
            _ambientColor = DataUtil.GetObject<Color>("AmbientColor") ?? Color.FromArgb(255,255,255,255);
            
            SceneBase scene;
            switch (_ambientShow) {
                case -1:
                    scene = new SceneSolid(_ambientColor);
                    break;
                case 0:
                    scene = new SceneRandom();
                    break;
                case 1:
                    scene = new SceneFire();
                    break;
                case 2:
                    scene = new SceneTwinkle();
                    break;
                case 3:
                    scene = new SceneOcean();
                    break;
                case 4:
                    scene = new SceneRainbow();
                    break;
                case 5:
                    scene = new SceneJuly();
                    break;
                case 6:
                    scene = new SceneHoliday();
                    break;
                case 7:
                    scene = new ScenePop();
                    break;
                case 8:
                    scene = new SceneForest();
                    break;
                default:
                    scene = null;
                    break;
            }

            if (scene == null) return;
            _colors = scene.GetColors();
            _animationTime = scene.AnimationTime;
            _mode = scene.Mode;
            _startInt = 0;
            RefreshColors(_colors);
        }
    }
}