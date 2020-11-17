using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using Glimmr.Models.DreamScreen.Scenes;
using Glimmr.Models.Services;
using Glimmr.Models.Util;
using Newtonsoft.Json;
using static Glimmr.Models.DreamScreen.Scenes.SceneBase;

namespace Glimmr.Models.DreamScreen {
    public class DreamScene {
        private double _animationTime;
        private string[] _colors;
        private AnimationMode _mode;
        private int _startInt;
        private int _vLedCount;
        private int _hLedCount;

        public void LoadScene(int sceneNo, int vLedCount = 0, int hLedCount = 0) {
            if (vLedCount == 0) vLedCount = 3;
            if (hLedCount == 0) hLedCount = 5;

            _hLedCount = hLedCount;
            _vLedCount = vLedCount;
            
            SceneBase scene;
            switch (sceneNo) {
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

        public void BuildColors(DreamClient dc, CancellationToken ct) {
            if (dc == null) throw new ArgumentNullException(nameof(dc));
            _startInt = 0;
            var startTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            LogUtil.WriteInc($@"Color builder started, animation time is {_animationTime}...");
            while (!ct.IsCancellationRequested) {
                var curTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                var dTime = curTime - startTime;
                // Check and set colors if time is greater than animation int, then reset time count...
                if (!(dTime > _animationTime * 1000)) continue;
                startTime = curTime;
                var cols = RefreshColors(_colors);
                var ledCols = new List<Color>();
                // Loop over r sectors and add to List
                var i = _vLedCount / 3;
                foreach (var v in new[] {0, 1, 2}) {
                    while (i > 0) {
                        ledCols.Add(cols[v]);
                        i--;
                    }
                }
                // Loop over t sectors and add to List
                i = _hLedCount / 5;
                foreach (var v in new[] {2, 3, 4, 5, 6}) {
                    while (i > 0) {
                        ledCols.Add(cols[v]);
                        i--;
                    }
                }
                // Loop over l sectors and add to List
                i = _vLedCount / 3;
                foreach (var v in new [] {6,7,8}) {
                    while (i > 0) {
                        ledCols.Add(cols[v]);
                        i--;
                    }
                }
                // Loop over b sectors and add to List
                i = _hLedCount / 5;
                foreach (var v in new [] {8,9,10,11,0}) {
                    while (i > 0) {
                        ledCols.Add(cols[v]);
                        i--;
                    }
                }
                dc.SendColors(ledCols, cols, _animationTime);
            }

            LogUtil.WriteDec($@"DreamScene: Color Builder canceled. {startTime}");
        }

        private List<Color> RefreshColors(string[] input) {
            var output = new List<Color>();
            var maxColors = input.Length - 1;
            var colorCount = _startInt;
            var col1 = _startInt;
            var allRand = new Random().Next(0, maxColors);
            for (var i = 0; i < 12; i++) {
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
    }
}