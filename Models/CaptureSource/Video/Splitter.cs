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
        private readonly List<Rectangle> _fullSectors;
        private readonly List<Rectangle> _fullSectorsV2;
        private int sourceWidth;
        private int sourceHeight;
        private int vCropCount = 0;
        private int hCropCount = 0;
        private int vCropPixels = 0;
        private int hCropPixels = 0;
        private bool vCrop;
        private bool hCrop;
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
        private const int MaxFrameCount = 75;
        
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
            
            if (vCrop || hCrop) {
                var vp = vCrop ? vCropPixels : 0;
                var hp = hCrop ? hCropPixels : 0;
                coords = DrawGrid(hp, vp);
                sectors = DrawSectors(hp, vp);
                sectorsV2 = DrawSectors(hp, vp, true);
            }
            
            if (DoSave) {
                DoSave = false;
                var path = Directory.GetCurrentDirectory();
                var gMat = new Mat();
                inputMat.CopyTo(gMat);
                var colInt = 0;
                var textColor = new Bgr(Color.White).MCvScalar;
                var previewCheck = 3;
                var sectorTarget = coords;
                var colorTarget = _colorsLed;
                if (previewCheck == 2) {
                    sectorTarget = sectors;
                    colorTarget = _colorsSectors;
                } else if (previewCheck == 3) {
                    sectorTarget = sectorsV2;
                    colorTarget = _colorsSectorsV2;
                }
                foreach (var s in sectorTarget) {
                    var scCol = colorTarget[colInt];
                    var stCol = ColorUtil.ClampAlpha(scCol);
                    var col = new Bgr(stCol).MCvScalar;
                    CvInvoke.Rectangle(gMat, s, col, -1, LineType.AntiAlias);
                    if (previewCheck != 1) {
                        var cInt = colInt + 1;
                        var tPoint = new Point(s.X, s.Y + 30);
                        CvInvoke.PutText(gMat, cInt.ToString(), tPoint, FontFace.HersheySimplex, 1.0, textColor);
                    }

                    colInt++;
                }
                gMat.Save(path + "/wwwroot/img/_preview_output.jpg");
                gMat.Dispose();
            }

            foreach (var sub in coords.Select(r => new Mat(_input, r))) {
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

       
        private static Color GetAverage(Mat sInput) {
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
                // Loop through half the rows, from top to bottom
                // These are our average values
                var cropHorizontal = 0;
                var cropVertical = 0;
                
                for (var r = 0; r < _input.Height / 4; r++) {
                    // This is the number of the bottom row to check
                    var r2 = _input.Height - r;
                    var s1 = new Rectangle(0,r,_input.Width,5);
                    var t1 = new Mat(_input, s1);
                    var t1Col = GetAverage(t1);
                    t1.Dispose();
                    if (!isBlack(t1Col)) continue;
                    var s2 = new Rectangle(0,r2-5,_input.Width,5);
                    var t2 = new Mat(_input, s2);
                    var t2Col = GetAverage(t2);
                    t2.Dispose();
                    if (isBlack(t2Col)) {
                        cropHorizontal = r;
                    }
                }
                
                for (var c = 0; c < _input.Width / 4; c++) {
                    // This is the number of the bottom row to check
                    var c2 = _input.Width - c;
                    var s1 = new Rectangle(c,0,5,_input.Height);
                    var t1 = new Mat(_input, s1);
                    var t1Col = GetAverage(t1);
                    t1.Dispose();
                    if (!isBlack(t1Col)) continue;
                    var s2 = new Rectangle(c2 - 5,0,1,_input.Height);
                    var t2 = new Mat(_input, s2);
                    var t2Col = GetAverage(t2);
                    t2.Dispose();
                    if (isBlack(t2Col)) {
                        cropVertical = c;
                    }
                }

                if (cropHorizontal != 0) {
                    hCropCount++;
                    if (Math.Abs(cropHorizontal - hCropPixels) < 5) {
                        if (hCropCount > MaxFrameCount) {
                            hCropCount = MaxFrameCount;
                            if (!hCrop) {
                                hCrop = true;
                                LogUtil.Write($"Enabling horizontal crop for {cropHorizontal} rows.");
                            }
                        }
                    } else {
                        if (hCrop) LogUtil.Write($"Adjusting horizontal crop to {hCropPixels} pixels.");
                        hCropPixels = cropHorizontal;
                    }
                    
                } else {
                    hCropCount--;
                    if (hCropCount < 0) {
                        hCropPixels = 0;
                        hCropCount = 0;
                        if (hCrop) {
                            hCrop = false;
                            LogUtil.Write("Disabling horizontal crop.");
                        }
                    }
                }

                if (cropVertical != 0) {
                    if (Math.Abs(cropVertical - vCropPixels) < 5) {
                        vCropCount++;
                        if (vCropCount > MaxFrameCount) {
                            vCropCount = MaxFrameCount;
                            if (!vCrop) {
                                vCrop = true;
                                LogUtil.Write($"Enabling vertical crop for {cropVertical} columns.");
                            }        
                        }
                    } else {
                        vCropPixels = cropVertical;
                        if (vCrop) LogUtil.Write($"Adjusting vertical crop to {vCropPixels} pixels.");
                    }
                } else {
                    vCropCount--;
                    if (vCropCount < 0) {
                        vCropCount = 0;
                        vCropPixels = 0;
                        if (vCrop) {
                            vCrop = false;
                            LogUtil.Write("Disabling vertical crop.");    
                        }
                        
                    }
                }
    
        }

        private static bool isBlack(Color color) {
            return color.R < 5 && color.G < 5 && color.B < 5;
        }

        private List<Rectangle> DrawGrid(double vOffset = 0, double hOffset = 0) {
            var output = new List<Rectangle>();
            
            // Top Region
            var tTop = vOffset;
            var tBottom = _borderHeight + tTop;

            // Bottom Gridlines
            var bBottom = sourceHeight - vOffset;
            var bTop = bBottom - _borderHeight;

            // Left Column Border
            var lLeft = hOffset;
            var lRight = hOffset + _borderWidth;

            // Right Column Border
            var rRight = sourceWidth - hOffset;
            var rLeft = rRight - _borderWidth;
            // Steps
            var tStep = sourceWidth / _hCount;
            var bStep = sourceWidth / _hCount;
            var lStep = sourceHeight / _vCount;
            var rStep = sourceHeight / _vCount;
            // Calc right regions, bottom to top
            var step = _vCount - 1;
            while (step >= 0) {
                var ord = step * rStep;
                var vw = rRight - rLeft;
                output.Add(new Rectangle((int) rLeft, ord, (int) vw, rStep));
                step--;
            }
            step = _hCount - 1;
            // Calc top regions, from right to left
            while (step >= 0) {
                var ord = step * tStep;
                var vw = tBottom - tTop;
                output.Add(new Rectangle(ord, (int) tTop,  tStep, (int) vw));
                step--;
            }

            step = 0;
            // Calc left regions (top to bottom)
            while (step < _vCount) {
                var ord = step * lStep;
                var vw = lRight - lLeft;
                output.Add(new Rectangle((int) lLeft, ord, (int) vw, lStep));
                step++;
            }

            step = 0;
            // Calc bottom regions (L-R)
            while (step < _hCount) {
                var ord = step * bStep;
                var vw = bBottom - bTop;
                output.Add(new Rectangle(ord, (int) bTop, bStep, (int) vw));
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
            // Individual segment sizes
            var sectorWidth =(int) (sourceWidth - hOffset * 2) / hSectorCount;
            var sectorHeight = (int) (sourceHeight - vOffset * 2) / vSectorCount;
            // These are based on the border/strip values
            // Minimum limits for top, bottom, left, right            
            var minTop = vOffset;
            var minBot = sourceHeight - vOffset - sectorHeight;
            var minLeft = hOffset;
            var minRight = sourceWidth - hOffset - sectorWidth;
            // Calc right regions, bottom to top
            var step = vSectorCount - 1;
            while (step >= 0) {
                var ord = step * sectorHeight + vOffset;
                fs.Add(new Rectangle((int) minRight, (int) ord, sectorWidth, sectorHeight));
                step--;
            }
            // Calc top regions, from right to left, skipping top-right corner (total horizontal sectors minus one)
            step = hSectorCount - 2;
            while (step >= 0) {
                var ord = step * sectorWidth + hOffset;
                fs.Add(new Rectangle((int) ord, (int) minTop, sectorWidth, sectorHeight));
                step--;
            }
            step = 1;
            // Calc left regions (top to bottom), skipping top-left
            while (step <= vSectorCount - 1) {
                var ord = step * sectorHeight + vOffset;
                fs.Add(new Rectangle((int) minLeft, (int) ord, sectorWidth, sectorHeight));
                step++;
            }
            step = 1;
            // Calc bottom center regions (L-R)
            while (step <= hSectorCount - 2) {
                var ord = step * sectorWidth + hOffset;
                fs.Add(new Rectangle((int) ord, (int) minBot, sectorWidth, sectorHeight));
                step += 1;
            }
            return fs;
        }
    }
}