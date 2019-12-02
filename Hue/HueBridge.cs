using HueDream.DreamScreen.Scenes;
using HueDream.HueDream;
using JsonFlatFileDataStore;
using Newtonsoft.Json;
using Q42.HueApi;
using Q42.HueApi.ColorConverters;
using Q42.HueApi.ColorConverters.HSB;
using Q42.HueApi.Interfaces;
using Q42.HueApi.Models.Bridge;
using Q42.HueApi.Models.Groups;
using Q42.HueApi.Streaming.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static HueDream.DreamScreen.Scenes.SceneBase;

namespace HueDream.Hue {
    public class HueBridge {
        public string bridgeIp { get; set; }
        public string bridgeKey { get; set; }
        public string bridgeUser { get; set; }
        public int Brightness { get; set; }
        public SceneBase sceneBase;
        public static bool doEntertain { set; get; }

        private readonly bool bridgeAuth;

        public List<LightMap> bridgeLights { get; set; }
        private string[] colors;
        private string[] prevColors;

        private EntertainmentLayer entLayer;


        public HueBridge() {
            DataStore dd = DreamData.getStore();
            Brightness = 100;
            bridgeIp = dd.GetItem("hueIp");
            bridgeKey = dd.GetItem("hueKey");
            bridgeUser = dd.GetItem("hueUser");
            bridgeAuth = dd.GetItem("hueAuth");
            bridgeLights = dd.GetItem<List<LightMap>>("hueMap");
            entLayer = null;
            sceneBase = null;
            dd.Dispose();
        }


        public void SetColors(string[] colorIn) {
            colors = colorIn;
        }




        public async Task StartStream(CancellationToken ct, DreamSync ds) {
            bridgeLights = DreamData.GetItem<List<LightMap>>("hueMap");
            Console.WriteLine("Hue: Connecting to bridge...");
            List<string> lights = new List<string>();
            foreach (LightMap lm in bridgeLights) {
                if (lm.SectorId != -1) {
                    lights.Add(lm.LightId.ToString());
                }
            }
            Console.WriteLine("Hue: Using Lights: " + JsonConvert.SerializeObject(lights));
            StreamingGroup stream = await StreamingSetup.SetupAndReturnGroup(lights, ct);
            Console.WriteLine("Hue: Stream established.");
            entLayer = stream.GetNewLayer(isBaseLayer: true);
            //Start automagically updating this entertainment group
            prevColors = colors;
            SendColorData(ct, ds);
        }

        private async Task SendColorData(CancellationToken ct, DreamSync ds) {
            if (entLayer != null) {
                Console.WriteLine("Hue: Bridge Connected. Beginning transmission...");
                Transition[] tList = new Transition[bridgeLights.Count];
                while (!ct.IsCancellationRequested) {

                    int lightInt = 0;
                    foreach (LightMap lightMap in bridgeLights) {
                        if (lightMap.SectorId != -1) {
                            int mapId = lightMap.SectorId;
                            string colorString = colors[mapId];

                            // Clamp our brightness based on settings
                            double bClamp = (255 * Brightness) / 100;
                            if (lightMap.OverrideBrightness) {
                                int newB = lightMap.Brightness;
                                bClamp = (255 * newB) / 100;
                            }
                            foreach (EntertainmentLight entLight in entLayer) {
                                if (entLight.Id == lightMap.LightId) {
                                    RGBColor oColor = new RGBColor(colorString);
                                    double sB = oColor.GetBrightness();
                                    HSB hsb = new HSB((int)oColor.GetHue(), (int)oColor.GetSaturation(), (int)oColor.GetBrightness());
                                    if (hsb.Brightness > bClamp) {
                                        hsb.Brightness = (int)bClamp;
                                    }
                                    oColor = hsb.GetRGB();
                                    double nB = oColor.GetBrightness();

                                    if (sceneBase != null) {
                                        EasingType easing = sceneBase.Easing;
                                        RGBColor sColor = new RGBColor(prevColors[mapId]);
                                        RGBColor tColor = oColor;
                                        double sBright = sColor.GetBrightness();
                                        double tBright = tColor.GetBrightness();
                                        double t1 = sceneBase.AnimationTime;
                                        double t2 = 0;

                                        switch (easing) {
                                            case EasingType.blend:
                                                sBright = (int)sColor.GetBrightness();
                                                break;
                                            case EasingType.fadeIn:
                                                sBright = 0;
                                                sColor = oColor;
                                                break;
                                            case EasingType.fadeOutIn:
                                                t1 = t1 / 2;
                                                t2 = t1;
                                                break;
                                            case EasingType.fadeOut:
                                                sBright = 0;
                                                sColor = oColor;
                                                break;
                                        }
                                        Transition oTrans = tList[lightInt];
                                        bool doTrans = true;
                                        if (oTrans != null) {
                                            if (!oTrans.IsFinished) {
                                                doTrans = false;
                                            }
                                        }
                                        if (doTrans) {
                                            Transition lTrans = new Transition(tColor, tBright, TimeSpan.FromSeconds(t1));
                                            entLight.Transition = lTrans;
                                            lTrans.Start(sColor, sBright, ct);
                                            prevColors[mapId] = oColor.ToHex();
                                            tList[lightInt] = lTrans;
                                        }
                                    } else {
                                        Console.WriteLine("No base scene foundd...");
                                        entLight.State.SetRGBColor(oColor);
                                        entLight.State.SetBrightness(Brightness);
                                    }
                                }
                            }
                        }
                        lightInt++;
                    }


                }
                Console.WriteLine("Hue: Token has been canceled.");
            } else {
                Console.WriteLine("Hue: Unable to fetch entertainment layer.");
            }
        }



        public async Task<Group[]> ListGroups() {
            LocalHueClient client = new LocalHueClient(bridgeIp, bridgeUser, bridgeKey);
            IReadOnlyList<Group> all = await client.GetEntertainmentGroups();
            List<Group> output = new List<Group>();
            output.AddRange(all);
            return output.ToArray();
        }

        public async Task<Light[]> ListLights() {
            LocalHueClient client = new LocalHueClient(bridgeIp, bridgeUser, bridgeKey);
            IEnumerable<Light> lList = new List<Light>();
            lList = await client.GetLightsAsync();
            return lList.ToArray();
        }



        public void StopEntertainment() {
            doEntertain = false;
            _ = StreamingSetup.StopStream().Result;
            Console.WriteLine("Hue: Entertainment closed and done.");
        }


        public async Task<RegisterEntertainmentResult> checkAuth() {
            RegisterEntertainmentResult result = null;
            try {
                ILocalHueClient client = new LocalHueClient(bridgeIp);
                //Make sure the user has pressed the button on the bridge before calling RegisterAsync
                //It will throw an LinkButtonNotPressedException if the user did not press the button
                result = await client.RegisterAsync("HueDream", Environment.MachineName, true);
                Console.WriteLine("Hue: User name is " + result.Username);
            } catch (Exception) {
                Console.WriteLine("Hue: The link button is not pressed.");
            }
            return result;
        }

        public static string findBridge() {
            string bridgeIp = "";
            Console.WriteLine("Hue: Looking for bridges");
            IBridgeLocator locator = new HttpBridgeLocator();
            IEnumerable<LocatedBridge> bridgeIPs = locator.LocateBridgesAsync(TimeSpan.FromSeconds(2)).Result;
            foreach (LocatedBridge bIp in bridgeIPs) {
                Console.WriteLine("Hue: Bridge IP is " + bIp.IpAddress);
                return bIp.IpAddress;
            }
            return bridgeIp;
        }

        public List<KeyValuePair<int, string>> getLights() {
            List<KeyValuePair<int, string>> lights = new List<KeyValuePair<int, string>>();
            if (bridgeIp != "0.0.0.0" && bridgeAuth) {
                ILocalHueClient client = new LocalHueClient(bridgeIp);
                client.Initialize(bridgeUser);
                Task<IEnumerable<Light>> task = Task.Run(async () => await client.GetLightsAsync().ConfigureAwait(false));
                IEnumerable<Light> lightArray = task.Result;
                foreach (Light light in lightArray) {
                    lights.Add(new KeyValuePair<int, string>(int.Parse(light.Id), light.Name));
                }
            }
            return lights;
        }

    }
}
