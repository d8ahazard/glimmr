using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using Glimmr.Models.ColorSource.Ambient.Scene;
using Glimmr.Models.LED;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
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
        private Color _ambientColor;
        private CancellationToken _ct;
        private ColorService _cs;
        private Stopwatch _watch;


        
        public AmbientStream(ColorService colorService, in CancellationToken ct) {
            _watch = new Stopwatch();
            _cs = colorService;
            _ct = ct;
            Refresh();
        }

        public void Initialize() {
            _startInt = 0;
            
            _watch.Start();
            Log.Debug($@"Color builder started, animation time is {_animationTime}...");
            while (!_ct.IsCancellationRequested) {
                if (!Streaming) continue;
                // Check and set colors if time is greater than animation int, then reset time count...
                if (!(_watch.ElapsedMilliseconds >= _animationTime * 1000)) continue;
                _watch.Restart();
                var cols = RefreshColors(_colors);
                var ledCols = new List<Color>();
                for (var i = 0; i < _ledCount; i++) {
                    var r = i / _ledCount * cols.Count;
                    ledCols.Add(cols[r]);
                }
                _cs.SendColors(ledCols, cols);
            }
            Log.Information("DreamScene: Color Builder canceled.");
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
            //Console.WriteLine($@"Returning refreshed colors: {JsonConvert.SerializeObject(output)}.");
            return output;
        }

        public bool Streaming { get; set; }
        public void ToggleSend(bool enable = true) {
            Streaming = enable;
        }

        
        public void Refresh() {
            LedData ld = DataUtil.GetObject<LedData>("LedData");
            _ledCount = ld.LedCount;
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
        }
    }
}