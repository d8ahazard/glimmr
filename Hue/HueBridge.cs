using HueDream.HueDream;
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
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace HueDream.Hue {
    public class HueBridge {
        public string bridgeIp { get; set; }
        public string bridgeKey { get; set; }
        public string bridgeUser { get; set; }

        public static bool doEntertain { set; get; }

        private readonly bool bridgeAuth;

        public List<KeyValuePair<int, int>> bridgeLights { get; }
        private string[] colors;

        private DataObj dreamData;
        private EntertainmentLayer entLayer;
        private Group entGroup;
        
        public HueBridge(DataObj dd) {
            dreamData = dd;
            bridgeIp = dd.HueIp;
            bridgeKey = dd.HueKey;
            bridgeUser = dd.HueUser;
            bridgeAuth = dd.HueAuth;
            bridgeLights = dd.HueMap;
            entGroup = dd.EntertainmentGroup;
            entLayer = null;
        }


        public void setColors(string[] colorIn) {
            colors = colorIn;
        }

        public async Task StartStream(CancellationToken ct) {
            Console.WriteLine("Connecting to bridge.");
            List<string> lights = new List<string>();
            foreach (KeyValuePair<int, int> kp in bridgeLights) {
                if (kp.Value != -1) lights.Add(kp.Key.ToString());
            }
            Console.WriteLine("Lights: " + JsonConvert.SerializeObject(lights));
            StreamingGroup stream = await StreamingSetup.SetupAndReturnGroup(dreamData, lights, ct);
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
                    double dBright = dreamData.DreamState.brightness;
                    foreach (KeyValuePair<int, int> lights in bridgeLights) {
                        if (lights.Value != -1) {
                            int mapId = lights.Value;
                            string colorString = colors[mapId];
                            foreach (EntertainmentLight light in entLayer) {
                                if (light.Id == lights.Key) {
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
            StreamingHueClient client = new StreamingHueClient(bridgeIp, bridgeUser, bridgeKey);
            //Get the entertainment group
            Console.WriteLine("LG");
            var all = await client.LocalHueClient.GetEntertainmentGroups();
            Console.WriteLine("Done");
            List<Group> output = new List<Group>();
            output.AddRange(all);
            Console.WriteLine("Returning");
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

        private bool ListContains(List<string> needle, List<string> haystack) {
            foreach (string s in needle) {
                if (!haystack.Contains(s)) {
                    return false;
                }
            }
            return true;
        }

        private async Task<string> ConfigureStreamingGroup(StreamingHueClient client) {
            string groupId = null;
            var lightList = new List<string>();

            foreach (KeyValuePair<int, int> kvp in bridgeLights) {
                if (kvp.Value != -1) {
                    lightList.Add(kvp.Key.ToString());
                }
            }

            // Fetch our entertainment groups
            var all = await client.LocalHueClient.GetEntertainmentGroups().ConfigureAwait(false);
            foreach (Group eg in all) {
                if (ListContains(lightList, eg.Lights)) {
                    Console.WriteLine("We've found a group with all of our lights in it.");
                    groupId = eg.Id;
                }
            }


            return groupId;
        }


        public void StopEntertainment() {
            Console.WriteLine("Stopping the e the hard way?");
            doEntertain = false;
        }


        public RegisterEntertainmentResult checkAuth() {
            try {
                ILocalHueClient client = new LocalHueClient(bridgeIp);
                //Make sure the user has pressed the button on the bridge before calling RegisterAsync
                //It will throw an LinkButtonNotPressedException if the user did not press the button
                var task = Task.Run(async () => await client.RegisterAsync("HueDream", System.Environment.MachineName, true));
                var appKey = task.Result;
                Console.WriteLine("Auth result: " + appKey.Username);
                return appKey;
            } catch (Exception e) {
                Console.WriteLine("Exception: " + e);
                return null;
            }
        }

        public static string findBridge() {
            string bridgeIp = "";
            Console.WriteLine("Looking for bridges");
            IBridgeLocator locator = new HttpBridgeLocator();
            var task = Task.Run(async () => await locator.LocateBridgesAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false));
            var bridgeIPs = task.Result;
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
                var task = Task.Run(async () => await client.GetLightsAsync().ConfigureAwait(false));
                var lightArray = task.Result;
                foreach (Light light in lightArray) {
                    if (light.Type == "Extended color light") {
                        lights.Add(new KeyValuePair<int, string>(int.Parse(light.Id), light.Name));
                    }
                }
            }
            return lights;
        }

    }
}
