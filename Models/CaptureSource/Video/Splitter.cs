using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Glimmr.Models.LED;
using Glimmr.Models.StreamingDevice.WLed;
using Glimmr.Models.Util;
using Newtonsoft.Json;

namespace Glimmr.Models.CaptureSource.Video {
    public class Splitter {
        private Mat _input;
        private readonly int _vCount;
        private readonly int _hCount;
        private List<Rectangle> _fullCoords;
        private List<Rectangle> _fullSectors;
        private List<Rectangle> _fullSectorsV2;
        public Dictionary<string, List<Rectangle>> WLSectors;
        public Dictionary<string, List<int>> WLModules;
        private int sourceWidth;
        private int sourceHeight;
        // Number of times our counts have matched
        private int _vCropCount;
        private int _hCropCount;
        // Current crop settings
        private int _vCropPixels;
        private int _hCropPixels;
        // Where we save the potential new value between checks
        private int _vCropCheck;
        private int _hCropCheck;
        // Are we cropping right now?
        private bool _vCrop;
        private bool _hCrop;
        // Set this when the sector changes
        private bool _sectorChanged;
        // The width of the border to crop from for LEDs
        private readonly float _borderWidth;
        private readonly float _borderHeight;
        // The current crop mode?
        private int _boxMode;
        private List<Color> _colorsLed;
        private List<Color> _colorsSectors;
        private List<Color> _colorsSectorsV2;
        private Dictionary<string, List<Color>> _colorsWled;
        private int _frameCount;
        private readonly float _brightBoost;
        private readonly float _saturationBoost;
        private readonly int _minBrightness;
        public bool DoSave;
        public bool NoImage;
        private const int MaxFrameCount = 30;
        
        public Splitter(LedData ld, int srcWidth, int srcHeight) {
            LogUtil.Write("Initializing splitter, using LED Data: " + JsonConvert.SerializeObject(ld));
            // Set some defaults, this should probably just not be null
            if (ld != null) {
                _vCount = ld.VCount;
                _hCount = ld.HCount;
            }

            sourceWidth = srcWidth;
            sourceHeight = srcHeight;
            // Set desired width of capture region to 15% total image
            _borderWidth = sourceWidth * .025f;
            _borderHeight = sourceHeight * .025f;
            // Set default box mode to zero, no boxing
            _boxMode = 0;
            // Set brightness boost, min brightness, sat boost...
            _brightBoost = 0;
            _minBrightness = 90;
            _saturationBoost = .2f;
            // Get sectors
            _fullCoords = DrawGrid();
            _fullSectors = DrawSectors();
            _fullSectorsV2 = DrawSectors(true);
            _frameCount = 0;
            _colorsWled = new Dictionary<string, List<Color>>();
            WLModules = new Dictionary<string, List<int>>();
            WLSectors = new Dictionary<string, List<Rectangle>>();
            LogUtil.Write("Splitter init complete.");
        }

        public void Update(Mat inputMat) {
            _input = inputMat ?? throw new ArgumentException("Invalid input material.");
            // Don't do anything if there's no frame.
            //LogUtil.Write("Updating splitter...");
            if (_input.IsEmpty) {
                LogUtil.Write("SPLITTER: NO INPUT.");
                return;
            }
            var outColorsStrip = new List<Color>();
            var outColorsSector = new List<Color>();
            var outColorsSectorV2 = new List<Color>();
            var outColorsWled = new Dictionary<string, List<Color>>();
            var wlSectors = WLSectors;
            if (_frameCount >= 10) {
                CheckSectors();
                _frameCount = 0;
            }
            _frameCount++;
            
            // Only calculate new sectors if the value has changed
            if (_sectorChanged) {
                _sectorChanged = false;
                var vp = _vCropPixels;
                var hp = _hCropPixels;
                LogUtil.Write($"No, really, adjusting crops to {vp} and {hp}");
                _fullCoords = DrawGrid();
                _fullSectors = DrawSectors();
                _fullSectorsV2 = DrawSectors( true);
                foreach (var id in WLModules.Keys) {
                    wlSectors[id] = DrawWledGrid(id);
                }
            }
            
            // Save a preview image if desired
            if (DoSave) {
                DoSave = false;
                var path = Directory.GetCurrentDirectory();
                var gMat = new Mat();
                inputMat.CopyTo(gMat);
                var colInt = 0;
                var textColor = new Bgr(Color.White).MCvScalar;
                var previewCheck = 1;
                var sectorTarget = _fullCoords;
                var colorTarget = _colorsLed;
                if (previewCheck == 2) {
                    sectorTarget = _fullSectors;
                    colorTarget = _colorsSectors;
                } else if (previewCheck == 3) {
                    sectorTarget = _fullSectorsV2;
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

            foreach (var sub in _fullCoords.Select(r => new Mat(_input, r))) {
                outColorsStrip.Add(GetAverage(sub));
                sub.Dispose();
            }

            foreach (var sub in _fullSectors.Select(r => new Mat(_input, r))) {
                outColorsSector.Add(GetAverage(sub));
                sub.Dispose();
            }

            foreach (var sub in _fullSectorsV2.Select(r => new Mat(_input, r))) {
                outColorsSectorV2.Add(GetAverage(sub));
                sub.Dispose();
            }

            foreach (var wlId in wlSectors.Keys) {
                var colorList = new List<Color>();
                foreach (var r in wlSectors[wlId]) {
                    if (0 <= r.X
                        && 0 <= r.Width
                        && r.X + r.Width <= _input.Cols
                        && 0 <= r.Y
                        && 0 <= _input.Height
                        && r.Y + r.Height <= _input.Rows){
                        using var sub = new Mat(_input, r);
                        // box within the image plane
                        colorList.Add(GetAverage(sub));
                    }
                }
                
                outColorsWled[wlId] = colorList;
            }
            _colorsLed = outColorsStrip;
            _colorsSectors = outColorsSector;
            _colorsSectorsV2 = outColorsSectorV2;
            _colorsWled = outColorsWled;
            //LogUtil.Write("Splitter updated...");
        }

        public void AddWled(WLedData wData) {
            LogUtil.Write("Adding wled to splitter.");
            // Create a simple list of vars that describe LED placement
            // Hcount, VCount, LedCount, Offset, Direction
            var ledVars = new List<int> {wData.HCount, wData.VCount, wData.LedCount, wData.Offset, wData.StripDirection};
            LogUtil.Write("LED Vars set: " + JsonConvert.SerializeObject(ledVars));
            // Store it for recall later
            WLModules[wData.Id] = ledVars;
            LogUtil.Write("Drawing grid...");
            // Generate our base grid sectors
            WLSectors[wData.Id] = DrawWledGrid(wData.Id);
            LogUtil.Write("WLED added.");
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

        public Dictionary<string, List<Color>> GetWledSectors() {
            return _colorsWled;
        }
        
        private void CheckSectors() {
            // Number of pixels to check per loop. Smaller = more accurate, larger = faster?
            const int checkSize = 1;
            // Set our starting values to zero per loop
            var cropHorizontal = 0;
            var cropVertical = 0;
            var gr = new Mat();
            CvInvoke.CvtColor(_input, gr,ColorConversion.Bgr2Gray);
            // Check to see if everything is black
            var allB = CvInvoke.CountNonZero(gr);
            NoImage = allB == 0;
            // If it is, we can stop here
            if (NoImage) {
                gr.Dispose();
                return;
            }
            // Loop through rows, checking for all black
            for (var r = 0; r <= _input.Height / 4; r+= checkSize) {
                // Define top position of bottom section
                var r2 = _input.Height - r - checkSize;   
                // Top Sector
                var s1 = new Rectangle(0,r,_input.Width,checkSize);
                // Make it a mat and check to see if it's black
                var t1 = new Mat(gr, s1);
                var n1 = CvInvoke.CountNonZero(t1);
                t1.Dispose();
                // If it isn't, we can stop here
                if (n1 != 0) continue;
                // If it is, check the corresponding bottom sector
                var s2 = new Rectangle(0,r2,_input.Width,checkSize);
                var t2 = new Mat(gr, s2);
                n1 = CvInvoke.CountNonZero(t2);
                t2.Dispose();
                // If the bottom is also black, save that value
                if (n1 == 0) {
                    cropHorizontal = r;
                }
            }
            
            for (var c = 0; c < _input.Width / 4; c+= checkSize) {
                // Define left coord of right sector
                var c2 = _input.Width - c - checkSize;
                // Create rect for left side check, make it a Mat
                var s1 = new Rectangle(c,0,checkSize, _input.Height);
                var t1 = new Mat(gr, s1);
                // See if it's all black
                var n1 = CvInvoke.CountNonZero(t1);
                t1.Dispose();
                // If not, stop here
                if (n1 != 0) continue;
                // Otherwise, do the same for the right side
                var s2 = new Rectangle(c2,0,1, _input.Height);
                var t2 = new Mat(gr, s2);
                n1 = CvInvoke.CountNonZero(t2);
                t2.Dispose();
                // If it is also black, set that to our current check value
                if (n1 == 0) {
                    cropVertical = c;
                }
            }
           
            gr.Dispose();
            // If we have a hcrop value
            if (cropHorizontal != 0) {
                // Check to see if we have the same crop value
                if (_hCropCheck == cropHorizontal) {
                    _hCropCount++;
                    // If it is, set to new value
                    // Now check to see if we've had the same value N times
                    if (_hCropCount > MaxFrameCount) {
                        _hCropCount = MaxFrameCount;
                        _hCropPixels = _hCropCheck;
                        // If we have, set the hCrop value and tell program to crop
                        if (!_hCrop) {
                            _sectorChanged = true;
                            _hCrop = true;
                            LogUtil.Write($"Enabling horizontal crop for {cropHorizontal} rows.");
                        }
                    }
                } else {
                    _hCropCheck = cropHorizontal;
                }
            } else {
                _hCropCount = -1;
            }
            
            //LogUtil.Write($"Hcropcount, new, check: {_hCropCount}, {cropHorizontal}, {_hCropCheck}");
            
            // If our crop count is lt zero, set it and the crop value to zero
            if (_hCropCount < 0) {
                _hCropPixels = 0;
                _hCropCount = 0;
                _hCropCheck = 0;
                if (_hCrop) {
                    _sectorChanged = true;
                    _hCrop = false;
                    LogUtil.Write("Disabling horizontal crop.");
                }
            }

            // If we have a vCrop value
            if (cropVertical != 0) {
                // Check to see if we have the same crop value
                if (_vCropCheck == cropVertical) {
                    _vCropCount++;
                    // If it is, set to new value
                    // Now check to see if we've had the same value N times
                    if (_vCropCount > MaxFrameCount) {
                        _vCropCount = MaxFrameCount;
                        _vCropPixels = _vCropCheck;
                        // If we have, set the vCrop value and tell program to crop
                        if (!_vCrop) {
                            _sectorChanged = true;
                            _vCrop = true;
                            LogUtil.Write($"Enabling Vertical crop for {cropVertical} rows.");
                        }
                    }
                } else {
                    _vCropCheck = cropVertical;
                }
            } else {
                _vCropCount = -1;
            }
            
            //LogUtil.Write($"vCropcount, new, check: {_vCropCount}, {cropVertical}, {_vCropCheck}");
            
            // If our crop count is lt zero, set it and the crop value to zero
            if (_vCropCount < 0) {
                _vCropPixels = 0;
                _vCropCount = 0;
                _vCropCheck = 0;
                if (_vCrop) {
                    _sectorChanged = true;
                    _vCrop = false;
                    LogUtil.Write("Disabling Vertical crop.");
                }
            }

    
        }

       
        private List<Rectangle> DrawGrid() {
            var vOffset = _vCropPixels;
            var hOffset = _hCropPixels;
            var output = new List<Rectangle>();
            
            // Top Region
            var tTop = hOffset;
            var tBottom = _borderHeight + tTop;

            // Bottom Gridlines
            var bBottom = sourceHeight - hOffset;
            var bTop = bBottom - _borderHeight;

            // Left Column Border
            var lLeft = vOffset;
            var lRight = vOffset + _borderWidth;

            // Right Column Border
            var rRight = sourceWidth - vOffset;
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
                output.Add(new Rectangle(ord, tTop,  tStep, (int) vw));
                step--;
            }

            step = 0;
            // Calc left regions (top to bottom)
            while (step < _vCount) {
                var ord = step * lStep;
                var vw = lRight - lLeft;
                output.Add(new Rectangle(lLeft, ord, (int) vw, lStep));
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
        
        private List<Rectangle> DrawWledGrid(string moduleId) {
            var hOffset = _hCropPixels;
            var vOffset = _vCropPixels;
            var output = new List<Rectangle>();
            var dims = WLModules[moduleId];
            LogUtil.Write("Drawing wled grid with dims: " + JsonConvert.SerializeObject(dims));
            // Hcount, VCount, LedCount, Offset, Direction
            var hCount = dims[0];
            var vCount = dims[1];
            var len = dims[2];
            var offset = dims[3];
            var dir = dims[4];
            if (hCount == 0 || vCount == 0 || len == 0) {
                LogUtil.Write("WLED needs to be configured first!");
                return null;
            }
            LogUtil.Write($"WL Splitter srcwidth and heigth are {sourceWidth} and {sourceHeight}");
            var bWidth = sourceWidth * .05f;
            var bHeight = sourceHeight * .05f;
            // Top Region
            var tTop = vOffset;
            var tBottom = bHeight + tTop;

            // Bottom Gridlines
            var bBottom = sourceHeight - vOffset;
            var bTop = bBottom - bHeight;

            // Left Column Border
            var lLeft = hOffset;
            var lRight = hOffset + bWidth;

            // Right Column Border
            var rRight = sourceWidth - hOffset;
            var rLeft = rRight - bWidth;
            // Steps
            var tStep = sourceWidth / hCount;
            var bStep = sourceWidth / hCount;
            var lStep = sourceHeight / vCount;
            var rStep = sourceHeight / vCount;
            // Calc right regions, bottom to top
            var step = vCount - 1;
            LogUtil.Write("Stepping...");
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
                output.Add(new Rectangle(ord, tTop,  tStep, (int) vw));
                step--;
            }

            step = 0;
            // Calc left regions (top to bottom)
            while (step < vCount) {
                var ord = step * lStep;
                var vw = lRight - lLeft;
                output.Add(new Rectangle(lLeft, ord, (int) vw, lStep));
                step++;
            }

            step = 0;
            // Calc bottom regions (L-R)
            while (step < hCount) {
                var ord = step * bStep;
                var vw = bBottom - bTop;
                output.Add(new Rectangle(ord, (int) bTop, bStep, (int) vw));
                step += 1;
            }

            LogUtil.Write("Truncating...");
            // Offset our colors based on info
            var truncated = new List<Rectangle>();
            if (dir == 0) {
                LogUtil.Write($"Dir0, offset is {offset}");
                var o = offset;
                while (o < output.Count && truncated.Count < len) {
                    //LogUtil.Write("Grabbing index " + o);
                    truncated.Add(output[o]);
                    o++;                }
                
                if (truncated.Count < len) {
                    var target = len - truncated.Count;
                    for (var i = 0; i < target && i < output.Count; i++) {
                        //LogUtil.Write("Grabbing index " + i);
                        truncated.Add(output[i]);
                    }
                }
            } else {
                LogUtil.Write("Dir1");
                var maxIdx = output.Count - 1;
                var o = maxIdx - offset;
                while (o > maxIdx - len) {
                    o--;
                    truncated.Add(output[o]);
                }

                if (truncated.Count < len) {
                    var target = len - truncated.Count;
                    for (var i = maxIdx; i >= maxIdx - target && i >= 0; i--) {
                        truncated.Add(output[i]);
                    }
                }
            }
            LogUtil.Write("Returning " + truncated.Count + " sectors for wled.");
            return truncated;
        }
        
        private List<Rectangle> DrawSectors(bool v2 = false) {
            // How many sectors does each region have?
            var hOffset = _hCropPixels;
            var vOffset = _vCropPixels;
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
            var sectorWidth =(sourceWidth - hOffset * 2) / hSectorCount;
            var sectorHeight = (sourceHeight - vOffset * 2) / vSectorCount;
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
                fs.Add(new Rectangle(minRight, ord, sectorWidth, sectorHeight));
                step--;
            }
            // Calc top regions, from right to left, skipping top-right corner (total horizontal sectors minus one)
            step = hSectorCount - 2;
            while (step >= 0) {
                var ord = step * sectorWidth + hOffset;
                fs.Add(new Rectangle(ord, minTop, sectorWidth, sectorHeight));
                step--;
            }
            step = 1;
            // Calc left regions (top to bottom), skipping top-left
            while (step <= vSectorCount - 1) {
                var ord = step * sectorHeight + vOffset;
                fs.Add(new Rectangle(minLeft, ord, sectorWidth, sectorHeight));
                step++;
            }
            step = 1;
            // Calc bottom center regions (L-R)
            while (step <= hSectorCount - 2) {
                var ord = step * sectorWidth + hOffset;
                fs.Add(new Rectangle(ord, minBot, sectorWidth, sectorHeight));
                step += 1;
            }
            return fs;
        }
    }
}