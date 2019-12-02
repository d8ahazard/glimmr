using HueDream.DreamScreen.Scenes;
using Newtonsoft.Json;
using System;
using System.Threading;
using System.Threading.Tasks;
using static HueDream.DreamScreen.Scenes.SceneBase;

namespace HueDream.DreamScreen {
    public class DreamScene {
        private string[] colorArray;
        private string[] targetArray;

        public string[] GetColorArray() {
            return colorArray;
        }

        private int startInt;
        private double animationTime;
        private string[] colors;
        private AnimationMode mode;

        public DreamScene() {
        }

        public SceneBase currentScene { get; set; }

        public void LoadScene(int sceneNumber) {
            SceneBase scene;
            switch (sceneNumber) {
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
            if (scene != null) {
                currentScene = scene;
                colors = scene.GetColors();
                animationTime = scene.AnimationTime;
                mode = scene.Mode;
                colorArray = RefreshColors(colors);
                targetArray = colorArray;
                startInt = 0;
            }
        }

        public async Task BuildColors(int sceneNumber, CancellationToken ct) {
            startInt = 0;
            Console.WriteLine("Loaded scene " + sceneNumber);
            long startTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            long curTime = startTime;
            // Set our colors right away
            Console.WriteLine("Initial calculation complete, starting loop.");
            while (!ct.IsCancellationRequested) {
                curTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                long dTime = curTime - startTime;
                // Check and set colors if time is greater than animation int, then reset time count...
                double tickPercent = dTime / (animationTime * 1000);
                if (dTime > animationTime * 1000) {
                    startTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    colorArray = RefreshColors(colors);
                    Console.WriteLine("TICK: " + JsonConvert.SerializeObject(colorArray));
                }
            }
            Console.WriteLine("Canceled??");
        }

        private string[] RefreshColors(string[] input) {
            string[] output = new string[12];
            int maxColors = input.Length - 1;
            int colorCount = startInt;
            int col1 = colorCount;
            int allRand = new Random().Next(0, maxColors);
            for (int i = 0; i < 12; i++) {
                col1 = i + col1;
                if (mode == AnimationMode.Random) {
                    col1 = new Random().Next(0, maxColors);
                }
                while (col1 > maxColors) {
                    col1 -= maxColors;
                }
                if (mode == AnimationMode.RandomAll) {
                    col1 = allRand;
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

    }
}
