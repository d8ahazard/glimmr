using HueDream.DreamScreen.Scenes;
using Newtonsoft.Json;
using System;
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
        private double animationTime;
        private string[] colors;
        private AnimationMode mode;

        public SceneBase CurrentScene { get; private set; }

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

            if (scene == null) return;
            CurrentScene = scene;
            colors = scene.GetColors();
            animationTime = scene.AnimationTime;
            mode = scene.Mode;
            colorArray = RefreshColors(colors);
            startInt = 0;
        }

        public async Task BuildColors(CancellationToken ct) {
            startInt = 0;
            Console.WriteLine(@"DreamScene: Loaded scene: {sceneNumber}.");
            var startTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            await Task.Run(() => {
                while (!ct.IsCancellationRequested) {
                    var curTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    var dTime = curTime - startTime;
                    // Check and set colors if time is greater than animation int, then reset time count...
                    if (!(dTime > animationTime * 1000)) continue;
                    startTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    colorArray = RefreshColors(colors);
                    Console.WriteLine($@"TICK: {JsonConvert.SerializeObject(colorArray)}.");
                }
            }, ct).ConfigureAwait(true);

            Console.WriteLine($@"DreamScene: Builder canceled. {startTime}");
        }

        private string[] RefreshColors(string[] input) {
            var output = new string[12];
            var maxColors = input.Length - 1;
            var colorCount = startInt;
            var col1 = colorCount;
            var allRand = new Random().Next(0, maxColors);
            for (var i = 0; i < 12; i++) {
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
            Console.WriteLine($@"Returning refreshed colors: {JsonConvert.SerializeObject(output)}.");
            return output;
        }

    }
}
