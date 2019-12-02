using HueDream.DreamScreen.Scenes;
using Newtonsoft.Json;
using Q42.HueApi.ColorConverters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static HueDream.DreamScreen.Scenes.SceneBase;

namespace HueDream.DreamScreen {
    public class DreamScene {
        private string[] colorArray;

        public string[] GetColorArray() {
            return colorArray;
        }

        private int startInt;
                
        public DreamScene() {
        }

        private SceneBase LoadScene(int sceneNumber) {
            switch (sceneNumber) {
                case 0:
                    return new SceneRandom();
                case 1:
                    return new SceneFire();
                case 2:
                    return new SceneTwinkle();
                case 3:
                    return new SceneOcean();
                case 4:
                    return new SceneRainbow();
                case 5:
                    return new SceneJuly();
                case 6:
                    return new SceneHoliday();
                case 7:
                    return new ScenePop();
                case 8:
                    return new SceneForest();
            }
            return null;
        }

        public async Task BuildColors(int sceneNumber, CancellationToken ct) {
            SceneBase scene = LoadScene(sceneNumber);
            string[] colors = scene.GetColors();
            double animationTime = scene.AnimationTime;
            AnimationMode mode = scene.Mode;
            EasingType easing = scene.Easing;
            startInt = 0;
            Console.WriteLine("Loaded scene " + sceneNumber);
            long startTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            long curTime = startTime;
            while (!ct.IsCancellationRequested) {
                curTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                long dTime = curTime - startTime;
                // Check and set colors if time is greater than animation int, then reset time count...
                if (dTime >= animationTime * 1000) {
                    startTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    colorArray = CalculateColors(RefreshColors(colors, mode), easing);
                    Console.WriteLine("TICK: " + JsonConvert.SerializeObject(colorArray));
                } else {
                    colorArray = CalculateColors(colorArray, easing);
                }
            }
            Console.WriteLine("Canceled??");
        }

        private string[] RefreshColors(string[] input, AnimationMode mode) {
            Console.WriteLine("Refreshing colors.");
            string[] output = new string[12];
            int maxColors = input.Length - 1;
            int colorCount = startInt;
            int col1 = colorCount;
            for (int i = 0; i < 12; i++) {
                col1 = i + col1;
                if (mode == AnimationMode.Random) {
                    col1 = new Random().Next(0, maxColors);
                }
                while (col1 > maxColors) {
                    col1 -= maxColors;
                    Console.WriteLine("Col1: " + col1);
                }
                output[i] = input[col1];
            }
            if (mode == AnimationMode.Linear) {
                startInt++;
            }

            if (mode == AnimationMode.Reverse) {
                startInt--;
            }
            if (startInt > maxColors) {
                startInt = 0;
            }

            if (startInt < 0) {
                startInt = maxColors;
            }
            Console.WriteLine("Returning refreshed colors: " + JsonConvert.SerializeObject(output));
            return output;
        }

        private string[] CalculateColors(string[] current, EasingType easing) {
            int i = 0;
            string[] output = current;
            /*foreach (string color in current) {
                switch (easing) {
                    case EasingType.none:
                        break;
                    case EasingType.blend:
                        break;
                    case EasingType.fadeIn:
                        break;
                    case EasingType.fadeOut:
                        break;
                    case EasingType.fadeOutIn:
                        break;
                }
                i++;
            }*/
            return output;
        }
    }
}
