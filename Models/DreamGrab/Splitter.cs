using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Emgu.CV;
using HueDream.Models.Util;
using Newtonsoft.Json;

namespace HueDream.Models.DreamGrab {
    public class Splitter {
        private Mat input;
        private int vCount;
        private int hCount;
        private List<Rectangle> fullCoords;
        private List<Rectangle> letterCoords;
        private List<Rectangle> pillarCoords;
        private List<Rectangle> boxCoords;
        private List<Rectangle> fullSectors;
        private List<Rectangle> letterSectors;
        private List<Rectangle> pillarSectors;
        private List<Rectangle> boxSectors;
        private List<Rectangle> checkRegions;
        private float border_width;
        private float border_height;
        private int letterCount;
        private int pillarCount;
        private int boxCount;
        private int boxMode;
        private Color[] gridColors;
        private Color[] gridSectors;
        private int fcount;
        private float brightBoost;
        private float saturationBoost;
        private int minBrightness;
        
        public Splitter(LedData ld, int srcWidth, int srcHeight) {
            vCount = ld.VCount;
            hCount = ld.HCount;
            border_width = srcWidth * .15625f;
            border_height = srcHeight * .20833333333f;
            letterCount = 0;
            pillarCount = 0;
            boxCount = 0;
            boxMode = 0;
            LogUtil.Write($@"Splitter init, {vCount}, {vCount}, {hCount}, {hCount}");
            brightBoost = 0;
            minBrightness = 90;
            saturationBoost = .2f;
            LogUtil.Write("Defaults loaded...");
            fullCoords = DrawGrid(srcWidth, srcHeight);
            fullSectors = DrawSectors(srcWidth, srcHeight);
            var vo = srcHeight * .12;
            // Check this
            var ho = srcWidth * .127;
            var lb = srcHeight - vo;
            var pr = srcWidth - ho;
            checkRegions = new List<Rectangle>();
            // Add letterbox regions
            checkRegions.Add(new Rectangle(0, 0, srcWidth, (int) vo));
            checkRegions.Add(new Rectangle(0, (int) lb, srcWidth, (int) vo));
            // Add pillarbox regions
            checkRegions.Add(new Rectangle(0, 0, (int) ho, srcHeight));
            checkRegions.Add(new Rectangle((int) pr, 0, (int) ho, srcHeight));
            letterCoords = DrawGrid(srcWidth, srcHeight, vo);
            letterSectors = DrawSectors(srcWidth, srcHeight, vo);
            pillarCoords = DrawGrid(srcWidth, srcHeight, 0, ho);
            pillarSectors = DrawSectors(srcWidth, srcHeight, 0, ho);
            boxCoords = DrawGrid(srcWidth, srcHeight, vo, ho);
            boxSectors = DrawSectors(srcWidth, srcHeight, vo, ho);
            fcount = 0;
            LogUtil.Write("Splitter should be created.");
        }

        public void Update(Mat inputMat) {
            input = inputMat;
            var output = new List<Color>();
            var output2 = new List<Color>();
            var coords = fullCoords;
            var sectors = fullSectors;
            if (fcount >= 10) {
                CheckSectors();
                fcount = 0;
            }
            fcount++;
            if (boxMode == 1) {
                coords = letterCoords;
                sectors = letterSectors;
            }

            if (boxMode == 2) {
                coords = pillarCoords;
                sectors = pillarSectors;
            }

            if (boxMode == 3) {
                coords = boxCoords;
                sectors = boxSectors;
            }
            
            foreach(var r in coords) {
                var sub = new Mat(input, r);
                output.Add(GetAverage(sub));
                sub.Dispose();
            }

            foreach (var r in sectors) {
                var sub = new Mat(input, r);
                output2.Add(GetAverage(sub));
                sub.Dispose();
            }

            gridColors = output.ToArray();
            gridSectors = output2.ToArray();
        }

        private Color GetAverage(Mat sInput) {
            var colors = CvInvoke.Mean(sInput);
            var cB = (int) colors.V0;
            var cG = (int) colors.V1;
            var cR = (int) colors.V2;
            if (cB + cG + cR < minBrightness) {
                cB = 0;
                cG = 0;
                cR = 0;
            }
            
            var outColor = Color.FromArgb(255,cR, cG, cB);
            if (brightBoost != 0.0) outColor = ColorUtil.BoostBrightness(outColor, brightBoost);
            if (saturationBoost != 0.0) outColor = ColorUtil.BoostSaturation(outColor, saturationBoost);
            
            return outColor;
        }


        
        
        public Color[] GetColors() {
            return gridColors;
        }
        
        public Color[] GetSectors() {
            return gridSectors;
        }

        private void CheckSectors() {
            // First, we need to get averages for pillar and letter sectors
            var colors = new List<Color>();
            var blk = Color.FromArgb(0,0,0);
            if (GetAverage(input) == blk) {
                LogUtil.Write("It appears we have no input.");
            } else {
                foreach (var nm in checkRegions.Select(sector => new Mat(input, sector))) {
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
            LogUtil.Write("Drawing grid...");
            var fc = new List<Rectangle>();
            LogUtil.Write("Lists created?");
            var h = srcHeight - vOffset * 2;
            var w = srcWidth - hOffset * 2;

            var tTop = vOffset;
            var tBott = border_height + tTop;

            // Bottom Gridlines
            var bTop = srcHeight - vOffset - border_height;
            var bBott = bTop + border_height;

            // Left Column Border
            var lLeft = hOffset;
            var lRight = hOffset + border_width;

            // Right Column Border
            var rLeft = srcWidth - hOffset - border_width;
            var rRight = rLeft + border_width;
            LogUtil.Write("Dafuq.");
            // Steps
            var tStep = w / hCount;
            var bStep = w / hCount;
            var lStep = h / vCount;
            var rStep = h / vCount;
            LogUtil.Write("Steps calculated...");
            // Calc right regions, bottom to top
            var step = vCount - 1;
            while (step >= 0) {
                var ord = step * rStep + vOffset;
                var vw = rRight - rLeft;
                fc.Add(new Rectangle((int) rLeft, (int) ord, (int) vw, step));
                step--;
            }
            LogUtil.Write("Done with right calc?");
            step = hCount - 1;
            // Calc top regions, from right to left
            while (step >= 0) {
                var ord = step * tStep + hOffset;
                var vw = tBott - tTop;
                fc.Add(new Rectangle((int) ord, (int) tTop, step, (int) vw));
                step--;
            }

            step = 0;
            // Calc left regions (top to bottom)
            while (step < vCount) {
                var ord = step * lStep + vOffset;
                var vw = lRight - lLeft;
                fc.Add(new Rectangle((int) lLeft, (int) ord, (int) vw, step));
                step++;
            }

            step = 0;
            // Calc bottom regions (L-R)
            while (step < hCount) {
                var ord = step * bStep + hOffset;
                var vw = bBott - bTop;
                fc.Add(new Rectangle((int) ord, (int) bTop, step, (int) vw));
                step += 1;
            }
            return fc;
        }
        
        private List<Rectangle> DrawSectors(int srcWidth, int srcHeight, double vOffset = 0, double hOffset = 0) {
            LogUtil.Write("Drawing sectors...");
            var fs = new List<Rectangle>();
            LogUtil.Write("Lists created?");
            var h = srcWidth - hOffset * 2;
            var v = srcHeight - vOffset * 2;
            var hWidth = h / 5;
            var vWidth = (int)border_width;
            var vHeight = v / 3;
            var hHeight = (int)border_height;
            
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
            // Calc top regions, from right to left, skipping topright corner
            while (step >= 0) {
                var ord = step * hWidth + hOffset;
                fs.Add(new Rectangle((int) ord, (int) minTop, (int) hWidth, hHeight));
                step--;
            }
            step = 1;
            // Calc left regions (top to bottom), skipping topleft
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
            LogUtil.Write("Sectors drawn, we have " + fs.Count + " items.");
            return fs;
        }
        
    }
}