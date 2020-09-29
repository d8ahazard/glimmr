using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Timers;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using HueDream.Models.LED;
using HueDream.Models.Util;
using Newtonsoft.Json;

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
        private readonly List<Rectangle> _fullSectorsV2;
        private readonly List<Rectangle> _letterSectorsV2;
        private readonly List<Rectangle> _pillarSectorsV2;
        private readonly List<Rectangle> _boxSectorsV2;
        private readonly List<Rectangle> _checkRegions;
        private int sourceWidth;
        private int sourceHeight;
        private int _countNoImg;
        private int _countLetterBox;
        private int _countPillarBox;
        private int _countFullBox;
        private readonly float _borderWidth;
        private readonly float _borderHeight;
        private int _boxMode;
        private List<Color> _colorsLed;
        private List<Color> _colorsSectors;
        private List<Color> _colorsSectorsV2;
        private int _frameCount;
        private readonly float _brightBoost;
        private readonly float _saturationBoost;
        private readonly int _minBrightness;
        public bool DoSave;
        private const int MaxFrameCount = 150;
        
        public Splitter(LedData ld, int srcWidth, int srcHeight) {
            // Set some defaults, this should probably just not be null
            if (ld != null) {
                _vCount = ld.VCount;
                _hCount = ld.HCount;
            }

            sourceWidth = srcWidth;
            sourceHeight = srcHeight;
            // Set desired width of capture region to 15% total image
            _borderWidth = sourceWidth * .05f;
            _borderHeight = sourceHeight * .05f;
            // Set default box mode to zero, no boxing
            _boxMode = 0;
            LogUtil.Write($@"Splitter init, {_vCount}, {_vCount}, {_hCount}, {_hCount}");
            LogUtil.Write($"Splitter src width and height are {srcWidth} and {srcHeight}.");
            // Set brightness boost, min brightness, sat boost...
            _brightBoost = 0;
            _minBrightness = 90;
            _saturationBoost = .2f;
            // Get sectors
            _fullCoords = DrawGrid();
            _fullSectors = DrawSectors();
            _fullSectorsV2 = DrawSectors(0,0, true);
            var lb = srcHeight - _borderHeight;
            var pr = srcWidth - _borderWidth;
            _checkRegions = new List<Rectangle> {
                new Rectangle(0, 0, srcWidth, (int) _borderHeight),
                new Rectangle(0, (int) lb, srcWidth, (int) _borderHeight),
                new Rectangle(0, 0, (int) _borderWidth, srcHeight),
                new Rectangle((int) pr, 0, (int) _borderWidth, srcHeight)
            };
            _letterCoords = DrawGrid(_borderHeight);
            _pillarCoords = DrawGrid(0, _borderWidth);
            _boxCoords = DrawGrid(_borderHeight, _borderWidth);
            _letterSectors = DrawSectors(_borderHeight);
            _pillarSectors = DrawSectors(0, _borderWidth);
            _boxSectors = DrawSectors(_borderHeight, _borderWidth);
            _letterSectorsV2 = DrawSectors(_borderHeight,0, true);
            _pillarSectorsV2 = DrawSectors(0, _borderWidth, true);
            _boxSectorsV2 = DrawSectors(_borderHeight, _borderWidth, true);
            _frameCount = 0;
            LogUtil.Write("Splitter should be created.");
        }

        public void Update(Mat inputMat) {
            _input = inputMat ?? throw new ArgumentException("Invalid input material.");
            // Don't do anything if there's no frame.
            if (_input.IsEmpty) return;
            var outputLed = new List<Color>();
            var outputSectors = new List<Color>();
            var outputSectorsV2 = new List<Color>();
            var coords = _fullCoords;
            var sectors = _fullSectors;
            var sectorsV2 = _fullSectorsV2;
            if (_frameCount >= 10) {
                CheckSectors();
                _frameCount = 0;
            }
            _frameCount++;
            switch (_boxMode) {
                // Letterbox
                case 1:
                    coords = _letterCoords;
                    sectors = _letterSectors;
                    sectorsV2 = _letterSectorsV2;
                    break;
                // Pillarbox
                case 2:
                    coords = _pillarCoords;
                    sectors = _pillarSectors;
                    sectorsV2 = _pillarSectorsV2;
                    break;
                // FullBox
                case 3:
                    coords = _boxCoords;
                    sectors = _boxSectors;
                    sectorsV2 = _boxSectorsV2;
                    break;
            }

            if (DoSave) {
                DoSave = false;
                var path = Directory.GetCurrentDirectory();
                var gMat = new Mat();
                inputMat.CopyTo(gMat);
                var colInt = 0;
                foreach (var r in coords) {
                    var scCol = _colorsLed[colInt];
                    var stCol = ColorUtil.ClampAlpha(scCol);
                    var col = new Bgr(stCol).MCvScalar;
                    CvInvoke.Rectangle(gMat,r, col, -1, LineType.AntiAlias);
                    colInt++;
                }
                gMat.Save(path + "/wwwroot/img/_preview_output.jpg");
                gMat.Dispose();
            }

            foreach (var r in coords) {
                //LogUtil.Write($"Trying to get coords from {_input.Width} and {_input.Height}: " + r);
                var sub = new Mat(_input, r);
                outputLed.Add(GetAverage(sub));
                sub.Dispose();
            }

            foreach (var sub in sectors.Select(r => new Mat(_input, r))) {
                outputSectors.Add(GetAverage(sub));
                sub.Dispose();
            }

            foreach (var sub in sectorsV2.Select(r => new Mat(_input, r))) {
                outputSectorsV2.Add(GetAverage(sub));
                sub.Dispose();
            }
            _colorsLed = outputLed;
            _colorsSectors = outputSectors;
            _colorsSectorsV2 = outputSectorsV2;
        }

       
        private Color GetAverage(Mat sInput) {
            var outColor = Color.Black;
            if (sInput.Cols == 0) return outColor;
            var colors = CvInvoke.Mean(sInput);
            var cB = (int) colors.V0;
            var cG = (int) colors.V1;
            var cR = (int) colors.V2;
            outColor = Color.FromArgb(cR, cG, cB);
            
            
            
            return outColor;
        }
        
        public List<Color> GetColors() {
            return _colorsLed;
        }
        
        public List<Color> GetSectors() {
            return _colorsSectors;
        }
        
        public List<Color> GetSectorsV2() {
            return _colorsSectorsV2;
        }

        private void CheckSectors() {
            // First, we need to get averages for pillar and letter sectors
            var colors = new List<Color>();
            var blk = Color.FromArgb(0,0,0);
            if (GetAverage(_input) == blk) {
                //LogUtil.Write("It appears we have no input.");
                _countNoImg++;
            } else {
                _countNoImg--;
                foreach (var nm in _checkRegions.Select(sector => new Mat(_input, sector))) {
                    colors.Add(GetAverage(nm));
                    nm.Dispose();
                }
                
                // First, check to see if it's fully boxed
                if (colors[0] == blk && colors[1] == blk && colors[2] == blk && colors[3] == blk) {
                    LogUtil.Write("This is fully boxed.");
                    _countFullBox++;
                } else {
                    // Otherwise, check to see if letter or pillar boxed.
                    _countFullBox--;
                    if (colors[0] == blk && colors[1] == blk) {
                        //LogUtil.Write("Letterbox detected.");
                        _countLetterBox++;
                    } else {
                        _countLetterBox--;
                    }
                    
                    if (colors[2] == blk && colors[3] == blk) {
                        LogUtil.Write("Pillar box detected.");
                        _countPillarBox++;
                    } else {
                        _countPillarBox--;
                    }
                }

                // Reset our counts to 0 so they don't go negative.
                if (_countNoImg < 0) _countNoImg = 0;
                if (_countFullBox < 0) _countFullBox = 0;
                if (_countLetterBox < 0) _countLetterBox = 0;
                if (_countPillarBox < 0) _countPillarBox = 0;
                
                // Now set pillar/letter flags
                if (_countNoImg <= MaxFrameCount) {
                    if (_countFullBox >= MaxFrameCount) {
                        LogUtil.Write("Full box set.");
                        if (_countFullBox > MaxFrameCount * 2) _countFullBox = MaxFrameCount * 2;
                        _boxMode = 3;
                        return;
                    }
                    
                    if (_countPillarBox > MaxFrameCount) {
                        LogUtil.Write("Enabling Pillarbox Mode.");
                        if (_countPillarBox > MaxFrameCount * 2) _countPillarBox = MaxFrameCount * 2;
                        _boxMode = 2;
                        return;
                    }
                    
                    if (_countLetterBox > MaxFrameCount) {
                        //LogUtil.Write("Enabling Letterbox Mode.");
                        if (_countLetterBox > MaxFrameCount * 2) _countLetterBox = MaxFrameCount * 2;
                        _boxMode = 1;
                        return;
                    }

                } else {
                    if (_countNoImg > MaxFrameCount * 2) _countNoImg = MaxFrameCount * 2;
                }

                // Set default mode only if nothing else is set.
                _boxMode = 0;
            }
        }

        private List<Rectangle> DrawGrid(double vOffset = 0, double hOffset = 0) {
            var output = new List<Rectangle>();
            var srcHeight = sourceHeight - vOffset * 2;
            var srcWidth = sourceWidth - hOffset * 2;

            // Top Region
            var tTop = vOffset;
            var tBottom = _borderHeight + tTop;

            // Bottom Gridlines
            var bTop = srcHeight - _borderHeight;
            var bBottom = srcHeight;

            // Left Column Border
            var lLeft = hOffset;
            var lRight = hOffset + _borderWidth;

            // Right Column Border
            var rLeft = srcWidth - _borderWidth;
            var rRight = srcWidth;
            // Steps
            var tStep = srcWidth / _hCount;
            var bStep = srcWidth / _hCount;
            var lStep = srcHeight / _vCount;
            var rStep = srcHeight / _vCount;
            // Calc right regions, bottom to top
            var step = _vCount - 1;
            while (step >= 0) {
                var ord = step * rStep + vOffset;
                var vw = rRight - rLeft;
                output.Add(new Rectangle((int) rLeft, (int) ord, (int) vw, (int) rStep));
                step--;
            }
            step = _hCount - 1;
            // Calc top regions, from right to left
            while (step >= 0) {
                var ord = step * tStep + hOffset;
                var vw = tBottom - tTop;
                output.Add(new Rectangle((int) ord, (int) tTop,  (int) tStep, (int) vw));
                step--;
            }

            step = 0;
            // Calc left regions (top to bottom)
            while (step < _vCount) {
                var ord = step * lStep + vOffset;
                var vw = lRight - lLeft;
                output.Add(new Rectangle((int) lLeft, (int) ord, (int) vw, (int) lStep));
                step++;
            }

            step = 0;
            // Calc bottom regions (L-R)
            while (step < _hCount) {
                var ord = step * bStep + hOffset;
                var vw = bBottom - bTop;
                output.Add(new Rectangle((int) ord, (int) bTop, (int) bStep, (int) vw));
                step += 1;
            }
            return output;
        }
        
        private List<Rectangle> DrawSectors(double vOffset = 0, double hOffset = 0, bool v2 = false) {
            // How many sectors does each region have?
            var vSectorCount = 3;
            var hSectorCount = 5;

            if (v2) {
                vSectorCount *= 2;
                hSectorCount *= 2;
            }
            // This is where we're saving our output
            var fs = new List<Rectangle>();
            // Calculate heights, minus offset for boxing
            var inputWidth = sourceWidth - hOffset * 2;
            var inputHeight = sourceHeight - vOffset * 2;
            // Individual segment sizes
            var hWidth = inputWidth / hSectorCount;
            var vHeight = inputHeight / vSectorCount;
            // These are based on the border/strip values
            var vWidth = (int)_borderWidth;
            var hHeight = (int)_borderHeight;
            // Minimum limits for top, bottom, left, right            
            var minTop = vOffset;
            var minBot = sourceHeight - hHeight - vOffset;
            var minLeft = hOffset;
            var minRight = sourceWidth - vWidth - hOffset;
            // Calc right regions, bottom to top
            var step = vSectorCount - 1;
            while (step >= 0) {
                var ord = step * vHeight + vOffset;
                fs.Add(new Rectangle((int) minRight, (int) ord, vWidth, (int) vHeight));
                step--;
            }
            // Calc top regions, from right to left, skipping top-right corner (total horizontal sectors minus one)
            step = hSectorCount - 1;
            while (step >= 0) {
                var ord = step * hWidth + hOffset;
                fs.Add(new Rectangle((int) ord, (int) minTop, (int) hWidth, hHeight));
                step--;
            }
            step = 1;
            // Calc left regions (top to bottom), skipping top-left
            while (step <= vSectorCount - 1) {
                var ord = step * vHeight + vOffset;
                fs.Add(new Rectangle((int) minLeft, (int) ord, vWidth, (int) vHeight));
                step++;
            }
            step = 1;
            // Calc bottom center regions (L-R)
            while (step <= hSectorCount - 2) {
                var ord = step * hWidth + hOffset;
                fs.Add(new Rectangle((int) ord, (int) minBot, (int) hWidth, hHeight));
                step += 1;
            }
            return fs;
        }
    }
}