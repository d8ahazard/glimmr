using HueDream.DreamScreenControl;
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
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HueDream.HueControl {
    public class HueBridge {
        public string bridgeIp { get; set; }
        public string bridgeKey { get; set; }
        public string bridgeUser { get; set; }

        public bool bridgeAuth;

        public List<KeyValuePair<int, string>> bridgeLights;
        public string[][] colors { get; set; }

        private static readonly HttpClient httpClient = new HttpClient();

        public HueBridge() {
            DreamData dd = new DreamData();
            bridgeIp = dd.HUE_IP;
            bridgeKey = dd.HUE_KEY;
            bridgeUser = dd.HUE_USER;
            bridgeAuth = dd.HUE_AUTH;
            bridgeLights = dd.HUE_MAP;
        }

        public async Task startEntertainment() {
            Console.WriteLine("Starting entertainment, " + bridgeIp + " " + bridgeUser + " " + bridgeKey);
            StreamingHueClient client = new StreamingHueClient(bridgeIp, bridgeUser, bridgeKey);
            var lightList = (from kvp in bridgeLights select kvp.Key.ToString()).Distinct().ToList();
            Console.WriteLine("Light List: " + JsonConvert.SerializeObject(lightList));
            //Get the entertainment group
            var all = await client.LocalHueClient.GetEntertainmentGroups();
            var targetGroup = "";
            foreach(Group eg in all) {
                if (eg.Name == "HueDream2") {
                    Console.WriteLine("Got our group");
                    targetGroup = eg.Id;
                } else {
                    Console.WriteLine("Group is " + eg.Name);
                }
            }

            if (targetGroup == "") {
                Console.WriteLine("Creating target group?");
                string group = "";
                try {
                    Dictionary<string, object> data = new Dictionary<string, object>();
                    data.Add("name", "HueDream2");
                    data.Add("lights", lightList);
                    data.Add("class", "Other");
                    data.Add("type", "Entertainment");
                    var content = JsonConvert.SerializeObject(data);
                    var response = await httpClient.PostAsync("http://" + bridgeIp + "/api/" + bridgeUser + "/groups/", new StringContent(content, Encoding.UTF8, "application/json"));
                    var responseString = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("Response: " + responseString);
                    var responseData = JToken.Parse(JsonConvert.SerializeObject(responseString));
                    if (responseData["Success"] != null) {
                        group = (string) responseData["Success"]["id"];

                    }
                } catch (ArgumentNullException e) {
                    Console.WriteLine("Null Exception!");
                }
                Console.WriteLine("Target group creation complete...");
                if (group != "") targetGroup = group;
            }
            //Create a streaming group
            if (targetGroup != "") {
                Group sg = await client.LocalHueClient.GetGroupAsync(targetGroup);
                Console.WriteLine("Creating streaming group: " + targetGroup);
                var entGroup = new StreamingGroup(sg.Locations);
                //Connect to the streaming group
                await client.Connect(targetGroup);
                Console.WriteLine("Connected.");
                //Start auto updating this entertainment group
                client.AutoUpdate(entGroup, CancellationToken.None);
                Console.WriteLine("AutoUpdate Enabled.");
                EntertainmentLayer entertainmentLayer = entGroup.GetNewLayer(isBaseLayer: true);
                while (true) {
                    Console.WriteLine("Looping Hue Bridge send..." + JsonConvert.SerializeObject(colors));
                    foreach(KeyValuePair<int, string> lights in bridgeLights) {
                        if (lights.Value != "-1") {
                            int mapId = Int32.Parse(lights.Value);
                            string[] colorString = colors[mapId];
                            Console.WriteLine("Color String for light" + mapId);
                            foreach(EntertainmentLight light in entertainmentLayer) {
                                Console.WriteLine("Light ID: " + light.Id);
                                if (light.Id == lights.Key) {
                                    string colorStrings = string.Join("", colorString);
                                    Console.WriteLine("We can map this: " + colorStrings);

                                    light.State.SetRGBColor(new RGBColor(colorStrings));
                                    light.State.SetBrightness(100);
                                }
                            }
                            //var light = entertainmentLayer.Where(x => x.Id == mapId).FirstOrDefault();
                            
                        }
                    }
                    
                }
                //foreach()
                //var l1 = entLayer.Where(x => x.Id == 1);
            } else {
                Console.WriteLine("Target Group creation failed!");
            }
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
            var task = Task.Run(async () => await locator.LocateBridgesAsync(TimeSpan.FromSeconds(5)));
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
                var task = Task.Run(async () => await client.GetLightsAsync());
                var lightArray = task.Result;
                foreach (Light light in lightArray) {
                    Console.WriteLine("Got a light");
                    if (light.Type == "Extended color light") {
                        Console.WriteLine("And it's colored");
                        lights.Add(new KeyValuePair<int, string>(Int32.Parse(light.Id), light.Name));
                    }
                }
            }
            return lights;
        }

    }
}
