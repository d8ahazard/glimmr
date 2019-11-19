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
        private string[][] colors;

        private static readonly HttpClient httpClient = new HttpClient();
        private StreamingHueClient client;
        private EntertainmentLayer entLayer;
        private Group streamingGroup;
        private string targetGroup;


        public HueBridge() {
            DreamData dd = new DreamData();            
            bridgeIp = dd.HUE_IP;
            bridgeKey = dd.HUE_KEY;
            bridgeUser = dd.HUE_USER;
            bridgeAuth = dd.HUE_AUTH;
            bridgeLights = dd.HUE_MAP;
            client = new StreamingHueClient(bridgeIp, bridgeUser, bridgeKey);
            entLayer = null;
            streamingGroup = null;
            targetGroup = null;
        }

        public string[][] getColors() {
            return colors;
        }

        public void setColors(string[][] colorIn) {
            colors = colorIn;
        }

        public void StartStream() {
            Console.WriteLine("StartStream fired.");
            if (!doEntertain) {
                doEntertain = true;
                // Connect to our stream?
                ConnectStream().Wait(5000);
                if (entLayer != null) {
                    Console.WriteLine("Starting da loop...");
                    while (doEntertain) {
                        foreach (KeyValuePair<int, string> lights in bridgeLights) {
                            if (lights.Value != "-1") {
                                int mapId = int.Parse(lights.Value);
                                string[] colorString = colors[mapId];
                                foreach (EntertainmentLight light in entLayer) {
                                    if (light.Id == lights.Key) {
                                        string colorStrings = string.Join("", colorString);
                                        light.State.SetRGBColor(new RGBColor(colorStrings));
                                        light.State.SetBrightness(100);
                                    }
                                }
                            }
                        }
                    }
                    Console.WriteLine("Token cancellation received by entertainment.");
                    client.LocalHueClient.SetStreamingAsync(targetGroup, false);
                    client.Close();
                    Console.WriteLine("Entertainment closed and done.");
                } else {
                    Console.WriteLine("Unable to fetch entertainment layer?");
                }
            }
        }

        public void StopStream() {
            Console.WriteLine("FFS");
            if (doEntertain) {
                doEntertain = false;
                Console.WriteLine("Entertainment: Force cancellation.");
                client.LocalHueClient.SetStreamingAsync(targetGroup, false);
                client.Close();
                Console.WriteLine("Entertainment2 closed and done.");
            }
        }

        public async Task ConnectStream() {
            Console.WriteLine("Connecting hue stream...");
            targetGroup = await ConfigureStreamingGroup(client);
            Group sg = await client.LocalHueClient.GetGroupAsync(targetGroup).ConfigureAwait(false);
            var entGroup = new StreamingGroup(sg.Locations);
            //Connect to the streaming group
            await client.Connect(targetGroup).ConfigureAwait(false);
            Console.WriteLine("Connected.");
            //Start manually updating this entertainment group
            client.AutoUpdate(entGroup, CancellationToken.None);
            Console.WriteLine("AutoUpdate Enabled.");
            entLayer = entGroup.GetNewLayer(isBaseLayer: true);
        }


        public async Task Entertain(CancellationToken token) {
            Console.WriteLine("Starting entertainment...");
            string targetGroup = await ConfigureStreamingGroup(client);
            Console.WriteLine("Target Group: " + targetGroup);
            //Create a streaming group
            if (!string.IsNullOrEmpty(targetGroup)) {
                Group sg = await client.LocalHueClient.GetGroupAsync(targetGroup).ConfigureAwait(false);
                var entGroup = new StreamingGroup(sg.Locations);
                //Connect to the streaming group
                await client.Connect(targetGroup).ConfigureAwait(false);
                Console.WriteLine("Connected.");
                //Start manually updating this entertainment group
                client.AutoUpdate(entGroup, token);
                Console.WriteLine("AutoUpdate Enabled.");
                EntertainmentLayer entertainmentLayer = entGroup.GetNewLayer(isBaseLayer: true);
                
                Console.WriteLine("Beginning entertainment stream.");
                //updateLoop(entertainmentLayer);
                while (doEntertain) {
                    foreach (KeyValuePair<int, string> lights in bridgeLights) {
                        if (lights.Value != "-1") {
                            int mapId = int.Parse(lights.Value);
                            string[] colorString = colors[mapId];
                            foreach (EntertainmentLight light in entertainmentLayer) {
                                if (light.Id == lights.Key) {
                                    string colorStrings = string.Join("", colorString);
                                    light.State.SetRGBColor(new RGBColor(colorStrings));
                                    light.State.SetBrightness(100);
                                }
                            }
                        }
                    }
                }
                
                Console.WriteLine("Token cancellation received by entertainment.");
                client.LocalHueClient.SetStreamingAsync(targetGroup, false);
                Console.WriteLine("Entertainment stream completed.");
            } else {
                Console.WriteLine("Target Group creation failed!");
            }
            client.Close();

        }

        private async Task<string> ConfigureStreamingGroup(StreamingHueClient client) {
            string groupId = null;
            var lightList = new List<string>();

            foreach (KeyValuePair<int, string> kvp in bridgeLights) {
                if (kvp.Value != "-1") {
                    lightList.Add(kvp.Key.ToString());
                }
            }

            // Build our data
            Dictionary<string, object> data = new Dictionary<string, object>();
            data.Add("name", "HueDream2");
            data.Add("lights", lightList);
            data.Add("class", "Other");
            Console.WriteLine("Light List: " + JsonConvert.SerializeObject(lightList));

            // Check if entertainment group exists
            var all = await client.LocalHueClient.GetEntertainmentGroups().ConfigureAwait(false);
            foreach (Group eg in all) {
                if (eg.Name == "HueDream2") {
                    Console.WriteLine("Entertainment Group found: " + eg.Id);
                    groupId = eg.Id;
                    break;
                }
            }

            try {
                if (string.IsNullOrEmpty(groupId)) {
                    Console.WriteLine("Creating Entertainment Group.");
                    string group = "";
                    data.Add("type", "Entertainment");
                    var content = JsonConvert.SerializeObject(data);
                    var response = await httpClient.PostAsync("http://" + bridgeIp + "/api/" + bridgeUser + "/groups/", new StringContent(content, Encoding.UTF8, "application/json")).ConfigureAwait(false);
                    var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var responseData = JToken.Parse(JsonConvert.SerializeObject(responseString));
                    if (responseData["Success"] != null) {
                        Console.WriteLine("Target group creation complete.");
                        groupId = (string)responseData["Success"]["id"];
                    } else {
                        Console.WriteLine("Group creation failed.");
                    }                                        
                } else {
                    Console.WriteLine("Updating Entertainment Group.");
                    var content = JsonConvert.SerializeObject(data);
                    var response = await httpClient.PutAsync("http://" + bridgeIp + "/api/" + bridgeUser + "/groups/" + groupId, new StringContent(content, Encoding.UTF8, "application/json"));
                    var responseString = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("ResponseString: " + responseString);                    
                }
            } catch (ArgumentNullException) {
                Console.WriteLine("Null Exception!");
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
