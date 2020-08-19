using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Emgu.CV;
using HueDream.Models.LED;
using HueDream.Models.Util;

namespace HueDream.Models.CaptureSource.Camera {
    public class Splitter {
        private Mat _input;
        private readonly int _vCount;
        private readonly int _hCount;
        private readonly List<Rectangle> _fullCoords;
        private readonly List<Rectangle> _letterCoords;
        private readonly List<Rectangle> _pillarCoords;
        private readonly List<Rectangle> _boxCoords;
        private readonly List<Rectangle> _fullSectors;
        private readonly List<Rectangle> _letterSectors;
        private readonly List<Rectangle> _pillarSectors;
        private readonly List<Rectangle> _boxSectors;
        private readonly List<Rectangle> _checkRegions;
        private readonly float _borderWidth;
        private readonly float _borderHeight;
        private readonly int _boxMode;
        private List<Color> _gridColors;
        private List<Color> _gridSectors;
        private int _frameCount;
        private readonly float _brightBoost;
        private readonly float _saturationBoost;
        private readonly int _minBrightness;
        
        public Splitter(LedData ld, int srcWidth, int srcHeight) {
            if (ld != null) {
                _vCount = ld.VCount;
                _hCount = ld.HCount;
            }
            _borderWidth = srcWidth * .15625f;
            _borderHeight = srcHeight * .20833333333f;
            _boxMode = 0;
            LogUtil.Write($@"Splitter init, {_vCount}, {_vCount}, {_hCount}, {_hCount}");
            _brightBoost = 0;
            _minBrightness = 90;
            _saturationBoost = .2f;
            LogUtil.Write("Defaults loaded...");
            _fullCoords = DrawGrid(srcWidth, srcHeight);
            _fullSectors = DrawSectors(srcWidth, srcHeight);
            var vo = srcHeight * .12;
            // Check this
            var ho = srcWidth * .127;
            var lb = srcHeight - vo;
            var pr = srcWidth - ho;
            _checkRegions = new List<Rectangle> {
                new Rectangle(0, 0, srcWidth, (int) vo),
                new Rectangle(0, (int) lb, srcWidth, (int) vo),
                new Rectangle(0, 0, (int) ho, srcHeight),
                new Rectangle((int) pr, 0, (int) ho, srcHeight)
            };
            // Add letterbox regions
            // Add pillar regions
            _letterCoords = DrawGrid(srcWidth, srcHeight, vo);
            _letterSectors = DrawSectors(srcWidth, srcHeight, vo);
            _pillarCoords = DrawGrid(srcWidth, srcHeight, 0, ho);
            _pillarSectors = DrawSectors(srcWidth, srcHeight, 0, ho);
            _boxCoords = DrawGrid(srcWidth, srcHeight, vo, ho);
            _boxSectors = DrawSectors(srcWidth, srcHeight, vo, ho);
            _frameCount = 0;
            LogUtil.Write("Splitter should be created.");
        }

        public void Update(Mat inputMat) {
            _input = inputMat;
            
            var output = new List<Color>();
            var output2 = new List<Color>();
            var coords = _fullCoords;
            var sectors = _fullSectors;
            if (_frameCount >= 10) {
                CheckSectors();
                _frameCount = 0;
            }
            _frameCount++;
            if (_boxMode == 1) {
                coords = _letterCoords;
                sectors = _letterSectors;
            }

            if (_boxMode == 2) {
                coords = _pillarCoords;
                sectors = _pillarSectors;
            }

            if (_boxMode == 3) {
                coords = _boxCoords;
                sectors = _boxSectors;
            }
            
            foreach(var r in coords) {
                var sub = new Mat(_input, r);
                output.Add(GetAverage(sub));
                sub.Dispose();
            }

            foreach (var r in sectors) {
                var sub = new Mat(_input, r);
                output2.Add(GetAverage(sub));
                sub.Dispose();
            }

            _gridColors = output;
            _gridSectors = output2;
        }

        private Color GetAverage(Mat sInput) {
            var colors = CvInvoke.Mean(sInput);
            var cB = (int) colors.V0;
            var cG = (int) colors.V1;
            var cR = (int) colors.V2;
            if (cB + cG + cR < _minBrightness) {
                cB = 0;
                cG = 0;
                cR = 0;
            }
            
            var outColor = Color.FromArgb(255,cR, cG, cB);
            if (Math.Abs(_brightBoost) > 0.01) outColor = ColorUtil.BoostBrightness(outColor, _brightBoost);
            if (Math.Abs(_saturationBoost) > 0.01) outColor = ColorUtil.BoostSaturation(outColor, _saturationBoost);
            
            return outColor;
        }


        
        
        public List<Color> GetColors() {
            return _gridColors;
        }
        
        public List<Color> GetSectors() {
            return _gridSectors;
        }

        private void CheckSectors() {
            // First, we need to get averages for pillar and letter sectors
            var colors = new List<Color>();
            var blk = Color.FromArgb(0,0,0);
            if (GetAverage(_input) == blk) {
                LogUtil.Write("It appears we have no input.");
            } else {
                foreach (var nm in _checkRegions.Select(sector => new Mat(_input, sector))) {
                    colors.Add(GetAverage(nm));
                    nm.Dispose();
                }

                if (colors[0] == blk && colors[1] == blk) {
                    LogUtil.Write("Letter sectors appear to be black.");
                }

                if (colors[2] == blk && colors[3] == blk) {
                    LogUtil.Write("Pillar sectors appear to be black.");
                }
            }
        }

        private List<Rectangle> DrawGrid(int srcWidth, int srcHeight, double vOffset = 0, double hOffset = 0) {
            var fc = new List<Rectangle>();
            var h = srcHeight - vOffset * 2;
            var w = srcWidth - hOffset * 2;

            var tTop = vOffset;
            var tBottom = _borderHeight + tTop;

            // Bottom Gridlines
            var bTop = srcHeight - vOffset - _borderHeight;
            var bBottom = bTop + _borderHeight;

            // Left Column Border
            var lLeft = hOffset;
            var lRight = hOffset + _borderWidth;

            // Right Column Border
            var rLeft = srcWidth - hOffset - _borderWidth;
            var rRight = rLeft + _borderWidth;
            // Steps
            var tStep = w / _hCount;
            var bStep = w / _hCount;
            var lStep = h / _vCount;
            var rStep = h / _vCount;
            // Calc right regions, bottom to top
            var step = _vCount - 1;
            while (step >= 0) {
                var ord = step * rStep + vOffset;
                var vw = rRight - rLeft;
                fc.Add(new Rectangle((int) rLeft, (int) ord, (int) vw, step));
                step--;
            }
            step = _hCount - 1;
            // Calc top regions, from right to left
            while (step >= 0) {
                var ord = step * tStep + hOffset;
                var vw = tBottom - tTop;
                fc.Add(new Rectangle((int) ord, (int) tTop, step, (int) vw));
                step--;
            }

            step = 0;
            // Calc left regions (top to bottom)
            while (step < _vCount) {
                var ord = step * lStep + vOffset;
                var vw = lRight - lLeft;
                fc.Add(new Rectangle((int) lLeft, (int) ord, (int) vw, step));
                step++;
            }

            step = 0;
            // Calc bottom regions (L-R)
            while (step < _hCount) {
                var ord = step * bStep + hOffset;
                var vw = bBottom - bTop;
                fc.Add(new Rectangle((int) ord, (int) bTop, step, (int) vw));
                step += 1;
            }
            return fc;
        }
        
        private List<Rectangle> DrawSectors(int srcWidth, int srcHeight, double vOffset = 0, double hOffset = 0) {
            var fs = new List<Rectangle>();
            var h = srcWidth - hOffset * 2;
            var v = srcHeight - vOffset * 2;
            var hWidth = h / 5;
            var vWidth = (int)_borderWidth;
            var vHeight = v / 3;
            var hHeight = (int)_borderHeight;
            
            var minTop = vOffset;
            var minBot = srcHeight - hHeight - vOffset;
            var minLeft = hOffset;
            var minRight = srcWidth - vWidth - hOffset;
            // Calc right regions, bottom to top
            var step = 2;
            while (step >= 0) {
                var ord = step * vHeight + vOffset;
                fs.Add(new Rectangle((int) minRight, (int) ord, vWidth, (int) vHeight));
                step--;
            }
            step = 3;
            // Calc top regions, from right to left, skipping top-right corner
            while (step >= 0) {
                var ord = step * hWidth + hOffset;
                fs.Add(new Rectangle((int) ord, (int) minTop, (int) hWidth, hHeight));
                step--;
            }
            step = 1;
            // Calc left regions (top to bottom), skipping top-left
            while (step <= 2) {
                var ord = step * vHeight + vOffset;
                fs.Add(new Rectangle((int) minLeft, (int) ord, vWidth, (int) vHeight));
                step++;
            }
            step = 1;
            // Calc bottom center regions (L-R)
            while (step <= 3) {
                var ord = step * hWidth + hOffset;
                fs.Add(new Rectangle((int) ord, (int) minBot, (int) hWidth, hHeight));
                step += 1;
            }
            return fs;
        }
        
    }
}