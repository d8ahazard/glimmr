using System.Collections.Generic;
using System.Drawing;
using Emgu.CV;
using Emgu.CV.Structure;
using HueDream.Models.Util;

namespace HueDream.Models.DreamGrab {
    public class Splitter {
        private int vCount;
        private int hCount;
        private List<Rectangle> areaCoords;
        private List<Rectangle> areaSectors;
        private int border_width = 20;
        private int border_height = 20;
        
        public Splitter(LedData ld, int srcWidth, int srcHeight) {
            vCount = ld.VCount;
            hCount = ld.HCount;
            LogUtil.Write($@"Splitter init, {vCount}, {vCount}, {hCount}, {hCount}");
            DrawGrid(srcWidth, srcHeight);
            DrawSectors(srcWidth, srcHeight);
            LogUtil.Write("Splitter should be created.");
        }

        private Color GetAverage(Mat input) {
            var colors = CvInvoke.Mean(input);
            return Color.FromArgb((int)colors.V0, (int)colors.V1, (int)colors.V2);
        }
        
        public Color[] GetColors(Mat input) {
            var output = new List<Color>();
            foreach(var r in areaCoords) {
                var sub = new Mat(input, r);
                output.Add(GetAverage(sub));
            }
            return output.ToArray();
        }
        
        public Color[] GetSectors(Mat input) {
            var output = new List<Color>();
            foreach(var r in areaSectors) {
                var sub = new Mat(input, r);
                output.Add(GetAverage(sub));
            }
            return output.ToArray();
        }

        private void DrawGrid(int srcWidth, int srcHeight) {
            LogUtil.Write("Drawing grid...");
            areaCoords = new List<Rectangle>();
            LogUtil.Write("Lists created?");
            var h = srcHeight;
            var w = srcWidth;

            var t_top = 0;
            var t_bott = border_height;

            // Bottom Gridlines
            var b_top = h - t_bott;
            var b_bott = h;

            // Left Column Border
            var l_left = 0;
            var l_right = border_width;

            // Right Column Border
            int r_left = w - l_right;
            var r_right = w;
            LogUtil.Write("Dafuq.");
            // Steps
            var t_step = w / hCount;
            var b_step = w / hCount;
            var l_step = h / vCount;
            var r_step = h / vCount;
            LogUtil.Write("Steps calculated...");
            // Calc right regions, bottom to top
            var step = vCount - 1;
            while (step >= 0) {
                var ord = step * r_step;
                areaCoords.Add(new Rectangle(r_left, ord, r_right - r_left, step));
                step--;
            }
            LogUtil.Write("Done with right calc?");
            step = hCount - 1;
            // Calc top regions, from right to left
            while (step >= 0) {
                var ord = step * t_step;
                areaCoords.Add(new Rectangle(ord, t_top, step, t_bott - t_top));
                step--;
            }

            step = 0;
            // Calc left regions (top to bottom)
            while (step < vCount) {
                var ord = step * l_step;
                areaCoords.Add(new Rectangle(l_left, ord, l_right - l_left, step));
                step++;
            }

            step = 0;
            // Calc bottom regions (L-R)
            while (step < hCount) {
                var ord = step * b_step;
                areaCoords.Add(new Rectangle(ord, b_top, step, b_bott - b_top));
                step += 1;
            }
            LogUtil.Write("Grid drawn, we have " + areaCoords.Count + " items.");
        }
        
        private void DrawSectors(int srcWidth, int srcHeight) {
            LogUtil.Write("Drawing sectors...");
            areaSectors = new List<Rectangle>();
            LogUtil.Write("Lists created?");
            var h = srcHeight;
            var w = srcWidth;

            var t_top = 0;
            var t_bott = border_height;

            // Bottom Gridlines
            var b_top = h - t_bott;
            var b_bott = h;

            // Left Column Border
            var l_left = 0;
            var l_right = border_width;

            // Right Column Border
            int r_left = w - l_right;
            var r_right = w;
            LogUtil.Write("Dafuq.");
            // Steps
            var h_step = w / 5;
            var v_step = h / 3;
            LogUtil.Write("Steps calculated...");
            // Calc right regions, bottom to top
            var step = 2;
            while (step >= 0) {
                var ord = step * v_step;
                areaSectors.Add(new Rectangle(r_left, ord, r_right - r_left, step));
                step--;
            }
            LogUtil.Write("Done with right calc?");
            step = 3;
            // Calc top regions, from right to left, skipping topright corner
            while (step >= 0) {
                var ord = step * h_step;
                areaSectors.Add(new Rectangle(ord, t_top, step, t_bott - t_top));
                step--;
            }
            LogUtil.Write("Done with top calc");
            step = 1;
            // Calc left regions (top to bottom), skipping topleft
            while (step < 3) {
                var ord = step * v_step;
                areaSectors.Add(new Rectangle(l_left, ord, l_right - l_left, step));
                step++;
            }
            LogUtil.Write("Done with left calc");
            step = 1;
            // Calc bottom regions (L-R)
            while (step < 5) {
                var ord = step * h_step;
                areaSectors.Add(new Rectangle(ord, b_top, step, b_bott - b_top));
                step += 1;
            }
            LogUtil.Write("Done with bottom calc");
            // Calculate middle (13-15)
            
            LogUtil.Write("Sectors drawn, we have " + areaSectors.Count + " items.");
        }
    }
}