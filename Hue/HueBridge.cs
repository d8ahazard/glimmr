using HueDream.DreamScreen.Scenes;
using HueDream.HueDream;
using Q42.HueApi;
using Q42.HueApi.ColorConverters;
using Q42.HueApi.ColorConverters.HSB;
using Q42.HueApi.Interfaces;
using Q42.HueApi.Models.Bridge;
using Q42.HueApi.Models.Groups;
using Q42.HueApi.Streaming.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static HueDream.DreamScreen.Scenes.SceneBase;

namespace HueDream.Hue {
    public class HueBridge {
        private string BridgeIp { get; }
        private string BridgeKey { get; }
        private string BridgeUser { get; }
        public int Brightness { get; set; }
        public SceneBase DreamSceneBase { get; set; }

        private readonly bool bridgeAuth;
      
        private string[] colors;
        private string[] prevColors;

        private EntertainmentLayer entLayer;
        private List<LightMap> lightMappings;
        private static readonly IFormatProvider Format = new CultureInfo("en-US"); 
        public HueBridge() {
            var dd = DreamData.GetStore();
            Brightness = 100;
            BridgeIp = dd.GetItem("hueIp");
            BridgeKey = dd.GetItem("hueKey");
            BridgeUser = dd.GetItem("hueUser");
            bridgeAuth = dd.GetItem("hueAuth");
            var lights = dd.GetItem<List<LightMap>>("hueMap");
            lightMappings = (from lm in lights where lm.SectorId != -1 select lm).ToList();
            entLayer = null;
            DreamSceneBase = null;
            dd.Dispose();
        }


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
            entLayer = stream.GetNewLayer(isBaseLayer: true);
            // Start automagically updating this entertainment group
            prevColors = colors;
            await SendColorData(ct).ConfigureAwait(false);
        }

        private async Task SendColorData(CancellationToken ct) {
            if (entLayer != null) {
                Console.WriteLine($@"Hue: Bridge Connected. Beginning transmission to {BridgeIp}...");
                var tList = new Transition[lightMappings.Count];
                
                await Task.Run(() => {
                    while (!ct.IsCancellationRequested) {
                        var lightInt = 0;
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
                            if (DreamSceneBase != null) {
                                // Our start color is the last color we had
                                var startColor = new RGBColor(prevColors[lightData.SectorId]);
                                // If fading in and out, set the start/end colors appropriately
                                switch (DreamSceneBase.Easing) {
                                    case EasingType.FadeIn:
                                        startColor = new RGBColor("000000");
                                        break;
                                    case EasingType.FadeOut:
                                        startColor = endColor;
                                        endColor = new RGBColor("000000");
                                        break;
                                }

                                // See if we already have a transition on our light
                                var currentTransition = tList[lightInt];
                                
                                // If we do have a transition and it's running, break for this light
                                if (currentTransition != null) {
                                    if (!currentTransition.IsFinished)
                                    {
                                        continue;
                                    }
                                }
                                
                                // Create a new transition if we aren't running one
                                var lTrans = new Transition(endColor, TimeSpan.FromSeconds(DreamSceneBase.AnimationTime));
                                
                                // Set and start it
                                entLight.Transition = lTrans;
                                lTrans.Start(startColor, startColor.GetBrightness(), ct);
                                
                                // Store for next loop
                                prevColors[lightData.SectorId] = endColor.ToHex();
                                tList[lightInt] = lTrans;
                            } else {
                                // Otherwise, if we're streaming, just set the color
                                entLight.State.SetRGBColor(endColor);
                                entLight.State.SetBrightness(Brightness);
                            }
                        }
                    }
                }, ct).ConfigureAwait(true);
                Console.WriteLine($@"Hue: Token has been canceled for {BridgeIp}.");
            } else {
                Console.WriteLine($@"Hue: Unable to fetch entertainment layer. {BridgeIp}");
            }
        }

        private RGBColor ClampBrightness(string colorString, LightMap lightMap) {
            var oColor = new RGBColor(colorString);
            // Clamp our brightness based on settings
            long bClamp = (255 * Brightness) / 100;
            if (lightMap.OverrideBrightness) {
                var newB = lightMap.Brightness;
                bClamp = (255 * newB) / 100;
            }
            var hsb = new HSB((int)oColor.GetHue(), (int)oColor.GetSaturation(), (int)oColor.GetBrightness());
            if (hsb.Brightness > bClamp) {
                hsb.Brightness = (int)bClamp;
            }
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


        public async Task<RegisterEntertainmentResult> CheckAuth() {
            RegisterEntertainmentResult result = null;
            try {
                ILocalHueClient client = new LocalHueClient(BridgeIp);
                //Make sure the user has pressed the button on the bridge before calling RegisterAsync
                //It will throw an LinkButtonNotPressedException if the user did not press the button
                result = await client.RegisterAsync("HueDream", Environment.MachineName, true).ConfigureAwait(true);
                Console.WriteLine($@"Hue: User name is {result.Username}.");
            } catch (HueException) {
                Console.WriteLine($@"Hue: The link button is not pressed at {BridgeIp}.");
            }
            return result;
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

        public List<KeyValuePair<int, string>> GetLights() {
            var lights = new List<KeyValuePair<int, string>>();
            // If we have no IP or we're not authorized, return
            if (BridgeIp == "0.0.0.0" || !bridgeAuth) return lights;
            // Create client
            ILocalHueClient client = new LocalHueClient(BridgeIp);
            client.Initialize(BridgeUser);
            // Get lights
            var task = Task.Run(async () => await client.GetLightsAsync().ConfigureAwait(false));
            var lightArray = task.Result;
            lights.AddRange(lightArray.Select(light => new KeyValuePair<int, string>(int.Parse(light.Id, Format), light.Name)));
            return lights;
        }

    }
}
