using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Q42.HueApi;
using Q42.HueApi.ColorConverters;
using Q42.HueApi.ColorConverters.Gamut;
using Q42.HueApi.ColorConverters.HSB;
using Q42.HueApi.Interfaces;
using Q42.HueApi.Models.Bridge;
using Q42.HueApi.Models.Gamut;
using Q42.HueApi.Models.Groups;
using Q42.HueApi.Streaming.Extensions;
using Q42.HueApi.Streaming.Models;

namespace HueDream.Models.Hue {
    public class HueBridge {
        private readonly BridgeData bd;
        private EntertainmentLayer entLayer;

        public HueBridge(BridgeData data) {
            bd = data;
            BridgeIp = bd.Ip;
            BridgeKey = bd.Key;
            BridgeUser = bd.User;
            entLayer = null;
            Console.WriteLine(@"Hue: Loading bridge: " + BridgeIp);
        }

        private string BridgeIp { get; }
        private string BridgeKey { get; }
        private string BridgeUser { get; }


        /// <summary>
        ///     Set up and create a new streaming layer based on our light map
        /// </summary>
        /// <param name="ct">A cancellation token.</param>
        public void EnableStreaming(CancellationToken ct) {
            // Get our light map and filter for mapped lights
            Console.WriteLine($@"Hue: Connecting to bridge at {BridgeIp}...");
            // Grab our stream
            var stream = StreamingSetup.SetupAndReturnGroup(bd, ct).Result;
            Console.WriteLine($@"Hue: Stream established at {BridgeIp}.");
            // This is what we actually need
            entLayer = stream.GetNewLayer(true);
            Console.WriteLine($@"Hue: Bridge Connected. Beginning transmission to {BridgeIp}...");
        }
        
        public void DisableStreaming() {
            StreamingSetup.StopStream(bd);
        }

        /// <summary>
        ///     Update lights in entertainment layer
        /// </summary>
        /// <param name="colors">An array of 12 colors corresponding to sector data</param>
        /// <param name="brightness">The general brightness of the device</param>
        /// <param name="ct">A cancellation token</param>
        /// <param name="fadeTime">Optional: how long to fade to next state</param>
        public void UpdateLights(string[] colors, int brightness, CancellationToken ct, double fadeTime = 0) {
            if (entLayer != null) {
                var lightMappings = bd.Lights;
                // Loop through lights in entertainment layer
                Console.WriteLine(@"Sending to bridge...");
                foreach (var entLight in entLayer) {
                    // Get data for our light from map
                    var lightData = lightMappings.SingleOrDefault(item => item.Id == entLight.Id.ToString());
                    // Return if not mapped
                    if (lightData == null) continue;
                    // Otherwise, get the corresponding sector color
                    var colorString = colors[lightData.TargetSector];
                    // Make it into a color
                    var endColor = ClampBrightness(colorString, lightData, brightness);
                    var xyColor = HueColorConverter.RgbToXY(endColor, CIE1931Gamut.PhilipsWideGamut);
                    endColor = HueColorConverter.XYToRgb(xyColor, GetLightGamut(lightData.ModelId));
                    // If we're currently using a scene, animate it
                    if (fadeTime != 0) // Our start color is the last color we had
                        entLight.SetState(ct, endColor, endColor.GetBrightness(),
                            TimeSpan.FromSeconds(fadeTime));
                    else // Otherwise, if we're streaming, just set the color
                        entLight.SetState(ct, endColor, endColor.GetBrightness());
                    //entLight.State.SetRGBColor(endColor);
                    //entLight.State.SetBrightness(endColor.GetBrightness());
                }
            }
            else {
                Console.WriteLine($@"Hue: Unable to fetch entertainment layer. {BridgeIp}");
            }
        }

        private RGBColor ClampBrightness(string colorString, LightData lightData, int brightness) {
            var oColor = new RGBColor(colorString);
            // Clamp our brightness based on settings
            long bClamp = 255 * brightness / 100;
            if (lightData.OverrideBrightness) {
                var newB = lightData.Brightness;
                bClamp = 255 * newB / 100;
            }

            var hsb = new HSB((int) oColor.GetHue(), (int) oColor.GetSaturation(), (int) oColor.GetBrightness());
            if (hsb.Brightness > bClamp) hsb.Brightness = (int) bClamp;
            oColor = hsb.GetRGB();

            return oColor;
        }

        private CIE1931Gamut GetLightGamut(string modelId) {
            /*"""Gets the correct color gamut for the provided model id.
            Docs: http://www.developers.meethue.com/documentation/supported-lights
            """*/
            string[] modelsA = {"LST001", "LLC010", "LLC011", "LLC012", "LLC006", "LLC007", "LLC013"};
            string[] modelsB = {"LCT001", "LCT007", "LCT002", "LCT003", "LLM001"};
            string[] modelsC = {"LCT010", "LCT014", "LCT011", "LLC020", "LST002"};
            if (Array.Exists(modelsA, element => element == modelId)) return CIE1931Gamut.ModelTypeA;

            if (Array.Exists(modelsB, element => element == modelId)) return CIE1931Gamut.ModelTypeB;

            return Array.Exists(modelsC, element => element == modelId)
                ? CIE1931Gamut.ModelTypeC
                : CIE1931Gamut.PhilipsWideGamut;
        }


        public async Task<List<Group>> ListGroups() {
            var client = new LocalHueClient(BridgeIp, BridgeUser, BridgeKey);
            var all = await client.GetEntertainmentGroups().ConfigureAwait(true);
            var output = new List<Group>();
            output.AddRange(all);
            return output;
        }


        public static async Task<RegisterEntertainmentResult> CheckAuth(string bridgeIp) {
            RegisterEntertainmentResult result;
            try {
                ILocalHueClient client = new LocalHueClient(bridgeIp);
                //Make sure the user has pressed the button on the bridge before calling RegisterAsync
                //It will throw an LinkButtonNotPressedException if the user did not press the button
                result = client.RegisterAsync("HueDream", Environment.MachineName, true).Result;
                Console.WriteLine($@"Hue: User name is {result.Username}.");
                return result;
            }
            catch (HueException) {
                Console.WriteLine($@"Hue: The link button is not pressed at {bridgeIp}.");
            }

            return null;
        }


        public static LocatedBridge[] FindBridges(int time = 2) {
            Console.WriteLine(@"Hue: Looking for bridges...");
            IBridgeLocator locator = new SsdpBridgeLocator();
            var res = locator.LocateBridgesAsync(TimeSpan.FromSeconds(time)).Result;
            Console.WriteLine($@"Result: {JsonConvert.SerializeObject(res)}");
            return res.ToArray();
        }

        public List<LightData> GetLights() {
            // If we have no IP or we're not authorized, return
            if (BridgeIp == "0.0.0.0" || BridgeUser == null || BridgeKey == null) return new List<LightData>();
            // Create client
            Console.WriteLine(@"Hue: Enumerating lights.");
            ILocalHueClient client = new LocalHueClient(BridgeIp);
            client.Initialize(BridgeUser);
            // Get lights
            var lights = bd.Lights;
            var res = client.GetLightsAsync().Result;
            var ld = res.Select(r => new LightData(r)).ToList();
            var output = new List<LightData>();
            foreach (var light in ld) {
                var add = true;
                foreach (var unused in lights.Where(oLight => oLight.Id == light.Id)) add = false;
                if (add) output.Add(light);
            }

            lights.AddRange(output);
            Console.WriteLine(@"Returning: " + JsonConvert.SerializeObject(lights));
            return lights;
        }
    }
}