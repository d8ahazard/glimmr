using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HueDream.DreamScreen.Scenes;
using HueDream.HueDream;
using Newtonsoft.Json;
using Q42.HueApi;
using Q42.HueApi.ColorConverters;
using Q42.HueApi.ColorConverters.HSB;
using Q42.HueApi.Interfaces;
using Q42.HueApi.Models.Bridge;
using Q42.HueApi.Models.Groups;
using Q42.HueApi.Streaming.Extensions;
using Q42.HueApi.Streaming.Models;

namespace HueDream.Hue {
    public class HueBridge {
        private string[] colors;

        private EntertainmentLayer entLayer;
        private List<LightMap> lightMappings;


        public HueBridge(BridgeData bd) {
            Brightness = 100;
            BridgeIp = bd.BridgeIp;
            BridgeKey = bd.BridgeKey;
            BridgeUser = bd.BridgeUser;
            var lightMap = bd.GetMap();
            lightMappings = (from lm in lightMap where lm.SectorId != -1 select lm).ToList();
            entLayer = null;
            ActiveScene = null;
        }

        private string BridgeIp { get; }
        private string BridgeKey { get; }
        private string BridgeUser { get; }
        public int Brightness { get; set; }
        public SceneBase ActiveScene { get; set; }


        public void SetColors(string[] colorIn) {
            colors = colorIn;
        }


        public async Task StartStream(CancellationToken ct) {
            // Get our light map and filter for mapped lights
            List<LightMap> lights = DreamData.GetItem<List<LightMap>>("hueMap");
            lightMappings = (from lm in lights where lm.SectorId != -1 select lm).ToList();
            Console.WriteLine($@"Hue: Connecting to bridge at {BridgeIp}...");
            // Grab our stream
            var stream = await StreamingSetup.SetupAndReturnGroup(ct).ConfigureAwait(true);
            Console.WriteLine($@"Hue: Stream established at {BridgeIp}.");
            entLayer = stream.GetNewLayer(true);
            // Start automagically updating this entertainment group
            await SendColorData(ct).ConfigureAwait(false);
        }

        private async Task SendColorData(CancellationToken ct) {
            if (entLayer != null) {
                Console.WriteLine($@"Hue: Bridge Connected. Beginning transmission to {BridgeIp}...");
                await Task.Run(() => {
                    var startTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    while (!ct.IsCancellationRequested) {
                        // Loop through lights in entertainment layer
                        foreach (var entLight in entLayer) {
                            // Get data for our light from map
                            var lightData = lightMappings.SingleOrDefault(item => item.LightId == entLight.Id);
                            // Return if not mapped
                            if (lightData == null) continue;
                            // Otherwise, get the corresponding sector color
                            var colorString = colors[lightData.SectorId];
                            // Make it into a color
                            var endColor = ClampBrightness(colorString, lightData);
                            
                            // If we're currently using a scene, animate it
                            if (ActiveScene != null) {
                                // Our start color is the last color we had
                                var nowTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                                var tDiff = nowTime - startTime;
                                if (!(tDiff >= ActiveScene.AnimationTime * 1000)) continue;
                                startTime = nowTime;
                                entLight.SetState(ct, endColor, endColor.GetBrightness(),
                                    TimeSpan.FromSeconds(ActiveScene.AnimationTime));
                            }
                            else {
                                // Otherwise, if we're streaming, just set the color
                                entLight.SetState(ct, endColor, endColor.GetBrightness());
                                //entLight.State.SetRGBColor(endColor);
                                //entLight.State.SetBrightness(endColor.GetBrightness());
                            }
                        }
                    }
                }).ConfigureAwait(true);
                Console.WriteLine($@"Hue: Token has been canceled for {BridgeIp}.");
            }
            else {
                Console.WriteLine($@"Hue: Unable to fetch entertainment layer. {BridgeIp}");
            }
        }

        private RGBColor ClampBrightness(string colorString, LightMap lightMap) {
            var oColor = new RGBColor(colorString);
            // Clamp our brightness based on settings
            long bClamp = 255 * Brightness / 100;
            if (lightMap.OverrideBrightness) {
                var newB = lightMap.Brightness;
                bClamp = 255 * newB / 100;
            }

            var hsb = new HSB((int) oColor.GetHue(), (int) oColor.GetSaturation(), (int) oColor.GetBrightness());
            if (hsb.Brightness > bClamp) hsb.Brightness = (int) bClamp;
            oColor = hsb.GetRGB();
            return oColor;
        }


        public async Task<Group[]> ListGroups() {
            var client = new LocalHueClient(BridgeIp, BridgeUser, BridgeKey);
            var all = await client.GetEntertainmentGroups().ConfigureAwait(true);
            var output = new List<Group>();
            output.AddRange(all);
            return output.ToArray();
        }


        public void StopEntertainment() {
            StreamingSetup.StopStream().ConfigureAwait(true);
            Console.WriteLine($@"Hue: Entertainment closed and done to {BridgeIp}.");
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

        public static string FindBridge() {
            Console.WriteLine(@"Hue: Looking for bridges...");
            IBridgeLocator locator = new HttpBridgeLocator();
            var bridgeIPs = locator.LocateBridgesAsync(TimeSpan.FromSeconds(2)).Result;
            foreach (var bIp in bridgeIPs) {
                Console.WriteLine($@"Hue: Bridge IP is {bIp.IpAddress}.");
                return bIp.IpAddress;
            }

            return string.Empty;
        }
        
        public static LocatedBridge[] FindBridges() {
            Console.WriteLine(@"Hue: Looking for bridges...");
            IBridgeLocator locator = new HttpBridgeLocator();
            var res = locator.LocateBridgesAsync(TimeSpan.FromSeconds(2)).Result;
            return res.ToArray();
        }

        public LightData[] GetLights() {
            // If we have no IP or we're not authorized, return
            if (BridgeIp == "0.0.0.0" || BridgeUser == null || BridgeKey == null) return Array.Empty<LightData>();
            // Create client
            Console.WriteLine(@"Hue: Enumerating lights.");
            ILocalHueClient client = new LocalHueClient(BridgeIp);
            client.Initialize(BridgeUser);
            // Get lights
            var res = client.GetLightsAsync().Result;
            var ld = res.Select(r => new LightData(r)).ToList();
            Console.WriteLine(@"Returning: " + JsonConvert.SerializeObject(ld));
            return ld.ToArray();
        }
    }
}