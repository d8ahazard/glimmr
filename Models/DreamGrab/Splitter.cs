using System.Collections.Generic;
using System.Drawing;
using Emgu.CV;
using Emgu.CV.Structure;
using HueDream.Models.Util;

namespace HueDream.Models.DreamGrab {
    public class Splitter {
        private int numLeft;
        private int numRight;
        private int numTop;
        private int numBottom;
        private List<Rectangle> areaCoords;
        private List<Rectangle> areaRects;
        private int border_width = 20;
        private int border_height = 20;
        
        public Splitter(LedData ld, int srcWidth, int srcHeight) {
            numLeft = ld.CountLeft;
            numRight = ld.CountRight;
            numBottom = ld.CountBottom;
            numTop = ld.CountTop;
            LogUtil.Write($@"Splitter init, {numLeft}, {numRight}, {numBottom}, {numTop}");
            DrawGrid(srcWidth, srcHeight);
        }

        private Color GetAverage(Mat input) {
            var colors = CvInvoke.Mean(input);
            return Color.FromArgb((int)colors.V0, (int)colors.V1, (int)colors.V2);
        }
        
        public Color[] GetColors(Mat input) {
            var output = new List<Color>();
            foreach(Rectangle r in areaCoords) {
                Mat sub = new Mat(input, r);
                output.Add(GetAverage(sub));
            }
            return output.ToArray();
        } 

        private void DrawGrid(int srcWidth, int srcHeight) {
            LogUtil.Write("Drawing grid...");
            areaRects = new List<Rectangle>();
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
            var t_step = w / numTop;
            var b_step = w / numBottom;
            var l_step = h / numLeft;
            var r_step = h / numRight;
            LogUtil.Write("Steps calculated...");
            // Calc right regions, bottom to top
            var step = numRight - 1;
            while (step >= 0) {
                var ord = step * r_step;
                areaCoords.Add(new Rectangle(r_left, ord, r_right - r_left, step));
                step--;
            }
            LogUtil.Write("Done with right calc?");
            step = numTop - 1;
            // Calc top regions, from right to left
            while (step >= 0) {
                var ord = step * t_step;
                areaCoords.Add(new Rectangle(ord, t_top, step, t_bott - t_top));
                step--;
            }

            step = 0;
            // Calc left regions (top to bottom)
            while (step < numLeft) {
                var ord = step * l_step;
                areaCoords.Add(new Rectangle(l_left, ord, l_right - l_left, step));
                step++;
            }

            step = 0;
            // Calc bottom regions (L-R)
            while (step < numBottom) {
                var ord = step * b_step;
                areaCoords.Add(new Rectangle(ord, b_top, step, b_bott - b_top));
                step += 1;
            }
            LogUtil.Write("Grid drawn, we have " + areaCoords.Count + " items.");
        }
    }
}