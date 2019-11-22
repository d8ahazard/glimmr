using HueDream.HueDream;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Q42.HueApi;
using Q42.HueApi.ColorConverters;
using Q42.HueApi.Interfaces;
using Q42.HueApi.Models.Bridge;
using Q42.HueApi.Models.Groups;
using Q42.HueApi.Streaming;
using Q42.HueApi.Streaming.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HueDream.HueControl {
    public class HueBridge {
        public string bridgeIp { get; set; }
        public string bridgeKey { get; set; }
        public string bridgeUser { get; set; }

        public static bool doEntertain { set; get; }

        private readonly bool bridgeAuth;

        public List<KeyValuePair<int, string>> bridgeLights { get; }
        private string[] colors;

        private DreamData dreamData;
        private StreamingHueClient client;
        private EntertainmentLayer entLayer;
        private StreamingGroup entGroup;
        private string targetGroup;
        private int brightness;


        public HueBridge(DreamData dd) {
            dreamData = dd;
            bridgeIp = dd.HUE_IP;
            bridgeKey = dd.HUE_KEY;
            bridgeUser = dd.HUE_USER;
            bridgeAuth = dd.HUE_AUTH;
            bridgeLights = dd.HUE_MAP;
            brightness = dd.DS_BRIGHTNESS;
            Console.WriteLine("Still alive");
            client = new StreamingHueClient(bridgeIp, bridgeUser, bridgeKey);
            entLayer = null;
            entGroup = null;
            targetGroup = null;
            GetGroups();
        }

        public string[] getColors() {
            return colors;
        }

        public void setColors(string[] colorIn) {
            colors = colorIn;
        }

        public async Task StartStream() {
            Console.WriteLine("Connecting to bridge.");
            if (!doEntertain) {
                doEntertain = true;
                // Connect to our stream?
                await client.Connect(targetGroup);
                Console.WriteLine("Connected.");
                //Start automagically updating this entertainment group
                client.AutoUpdate(entGroup, CancellationToken.None);
                if (entLayer != null) {
                    Console.WriteLine("Connected! Beginning transmission...");
                    int frame = 0;
                    while (doEntertain) {
                        double dBright = dreamData.DS_BRIGHTNESS;                        
                        foreach (KeyValuePair<int, string> lights in bridgeLights) {
                            if (lights.Value != "-1") {
                                int mapId = int.Parse(lights.Value);
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
                    client.LocalHueClient.SetStreamingAsync(targetGroup, false);
                    Console.WriteLine("Entertainment closed and done.");
                } else {
                    Console.WriteLine("Unable to fetch entertainment layer?");
                }
            }
        }

        public void StopStream() {
            Console.WriteLine("FFS");            
        }



        private async void GetGroups() {
            if (entGroup == null) {
                if (targetGroup == null) targetGroup = await ConfigureStreamingGroup(client);
                Console.WriteLine("Target Group is " + targetGroup);
                // Get our actual group object?
                Group sg = await client.LocalHueClient.GetGroupAsync(targetGroup).ConfigureAwait(false);
                Console.WriteLine("Streaming group Retreived.");
                entGroup = new StreamingGroup(sg.Locations);
                Console.WriteLine("Ent group created.");
            }
            //Connect to the streaming group

            if (entLayer == null) {
                Console.WriteLine("AutoUpdate Enabled.");
                entLayer = entGroup.GetNewLayer(isBaseLayer: true);
                Console.WriteLine("Entertainment layer is set.");
            }
        }
       
        private bool ListContains(List<string> needle, List<string>haystack) {
            foreach(string s in needle) {
                if (!haystack.Contains(s)) {
                    return false;
                }
            }
            return true;
        }

        private async Task<string> ConfigureStreamingGroup(StreamingHueClient client) {
            string groupId = null;
            var lightList = new List<string>();

            foreach (KeyValuePair<int, string> kvp in bridgeLights) {
                if (kvp.Value != "-1") {
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
