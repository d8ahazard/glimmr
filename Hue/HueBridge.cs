using HueDream.HueDream;
using JsonFlatFileDataStore;
using Newtonsoft.Json;
using Q42.HueApi;
using Q42.HueApi.ColorConverters;
using Q42.HueApi.Interfaces;
using Q42.HueApi.Models.Bridge;
using Q42.HueApi.Models.Groups;
using Q42.HueApi.Streaming;
using Q42.HueApi.Streaming.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HueDream.Hue {
    public class HueBridge {
        public string bridgeIp { get; set; }
        public string bridgeKey { get; set; }
        public string bridgeUser { get; set; }

        public static bool doEntertain { set; get; }

        private readonly bool bridgeAuth;

        public List<LightMap> bridgeLights { get; }
        private string[] colors;

        private readonly DataObj dreamData;
        private EntertainmentLayer entLayer;

        public HueBridge() {
            DataStore dd = DreamData.getStore();
            bridgeIp = dd.GetItem("hueIp");
            bridgeKey = dd.GetItem("hueKey");
            bridgeUser = dd.GetItem("hueUser");
            bridgeAuth = dd.GetItem("hueAuth");
            bridgeLights = dd.GetItem<List<LightMap>>("hueMap");
            entLayer = null;
            dd.Dispose();
        }


        public void setColors(string[] colorIn) {
            colors = colorIn;
        }

        public async Task StartStream(CancellationToken ct) {
            Console.WriteLine("Connecting to bridge.");
            List<string> lights = new List<string>();
            foreach (LightMap lm in bridgeLights) {
                if (lm.SectorId != -1) {
                    lights.Add(lm.LightId.ToString());
                }
            }
            Console.WriteLine("Lights: " + JsonConvert.SerializeObject(lights));
            StreamingGroup stream = await StreamingSetup.SetupAndReturnGroup(lights, ct);
            Console.WriteLine("Got stream.");
            entLayer = stream.GetNewLayer(isBaseLayer: true);
            // Connect to our stream?
            Console.WriteLine("Connected.");
            //Start automagically updating this entertainment group
            Console.WriteLine("Sending Color Data.");
            SendColorData(ct);
        }

        private async Task SendColorData(CancellationToken ct) {
            if (entLayer != null) {
                Console.WriteLine("Connected! Beginning transmission...");
                while (!ct.IsCancellationRequested) {
                    double dBright = dreamData.MyDevice.Brightness;
                    foreach (LightMap lights in bridgeLights) {
                        if (lights.SectorId != -1) {
                            int mapId = lights.SectorId;
                            string colorString = colors[mapId];
                            foreach (EntertainmentLight light in entLayer) {
                                if (light.Id == lights.LightId) {
                                    string colorStrings = colorString;
                                    light.State.SetRGBColor(new RGBColor(colorStrings));
                                    light.State.SetBrightness(dBright);
                                }
                            }
                        }
                    }
                }
                // Stop streaming?
                Console.WriteLine("Entertainment closed and done.");
            } else {
                Console.WriteLine("Unable to fetch entertainment layer?");
            }
        }



        public async Task<Group[]> ListGroups() {
            LocalHueClient client = new LocalHueClient(bridgeIp, bridgeUser, bridgeKey);
            //Get the entertainment group
            Console.WriteLine("LG");
            IReadOnlyList<Group> all = await client.GetEntertainmentGroups();
            Console.WriteLine("Done");
            List<Group> output = new List<Group>();
            output.AddRange(all);
            Console.WriteLine("Returning: " + JsonConvert.SerializeObject(output));
            return output.ToArray();
        }

        public async Task<Light[]> ListLights() {
            LocalHueClient client = new LocalHueClient(bridgeIp, bridgeUser, bridgeKey);
            //Get the entertainment group
            IEnumerable<Light> lList = new List<Light>();
            lList = await client.GetLightsAsync();
            Console.WriteLine("Returning");
            return lList.ToArray();
        }

        

        public void StopEntertainment() {
            Console.WriteLine("Stopping the e the hard way?");
            doEntertain = false;
        }


        public async Task<RegisterEntertainmentResult> checkAuth() {
            RegisterEntertainmentResult result = null;
            try {
                ILocalHueClient client = new LocalHueClient(bridgeIp);
                //Make sure the user has pressed the button on the bridge before calling RegisterAsync
                //It will throw an LinkButtonNotPressedException if the user did not press the button
                result = await client.RegisterAsync("HueDream", Environment.MachineName, true);
                Console.WriteLine("Auth result: " + result.Username);
            } catch (Exception) {
                Console.WriteLine("The link button is not pressed.");
            }
            return result;
        }

        public static string findBridge() {
            string bridgeIp = "";
            Console.WriteLine("Looking for bridges");
            IBridgeLocator locator = new HttpBridgeLocator();
            Task<IEnumerable<LocatedBridge>> task = Task.Run(async () => await locator.LocateBridgesAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false));
            IEnumerable<LocatedBridge> bridgeIPs = task.Result;
            foreach (LocatedBridge bIp in bridgeIPs) {
                Console.WriteLine("Bridge IP is " + bIp.IpAddress);
                return bIp.IpAddress;
            }
            Console.WriteLine("Done Looking for bridges");
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
