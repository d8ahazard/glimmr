using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HueDream.Models.Util;
using Q42.HueApi;
using Q42.HueApi.ColorConverters;
using Q42.HueApi.ColorConverters.HSB;
using Q42.HueApi.Interfaces;
using Q42.HueApi.Models.Bridge;
using Q42.HueApi.Models.Groups;
using Q42.HueApi.Streaming;
using Q42.HueApi.Streaming.Extensions;
using Q42.HueApi.Streaming.Models;

namespace HueDream.Models.Hue {
    public sealed class HueBridge : IDisposable {
        private readonly BridgeData bd;
        private EntertainmentLayer entLayer;
        private readonly StreamingHueClient client;
        private bool disposed;
        private bool streaming;

        public HueBridge(BridgeData data) {
            bd = data ?? throw new ArgumentNullException(nameof(data));
            BridgeIp = bd.IpAddress;
            BridgeKey = bd.Key;
            BridgeUser = bd.User;
            client = StreamingSetup.GetClient(bd);
            disposed = false;
            streaming = false;
            entLayer = null;
            LogUtil.Write(@"Hue: Loading bridge: " + BridgeIp);
        }

        private string BridgeIp { get; }
        private string BridgeKey { get; }
        private string BridgeUser { get; }


        /// <summary>
        ///     Set up and create a new streaming layer based on our light map
        /// </summary>
        /// <param name="ct">A cancellation token.</param>
        public bool EnableStreaming(CancellationToken ct) {
            // Get our light map and filter for mapped lights
            LogUtil.Write($@"Hue: Connecting to bridge at {BridgeIp}...");
            // Grab our stream
            var stream = StreamingSetup.SetupAndReturnGroup(client, bd, ct).Result;
            // This is what we actually need
            if (stream == null) return false;
            entLayer = stream.GetNewLayer(true);
            LogUtil.WriteInc($"Starting Hue Stream: {BridgeIp}");
            streaming = true;
            return streaming;
        }

        public void DisableStreaming() {
            var _ = StreamingSetup.StopStream(client, bd);
            LogUtil.WriteDec($"Stopping Hue Stream: {BridgeIp}");
            streaming = false;
        }

        /// <summary>
        ///     Update lights in entertainment layer
        /// </summary>
        /// <param name="colors">An array of 12 colors corresponding to sector data</param>
        /// <param name="brightness">The general brightness of the device</param>
        /// <param name="ct">A cancellation token</param>
        /// <param name="fadeTime">Optional: how long to fade to next state</param>
        public void UpdateLights(List<Color> colors, int brightness, CancellationToken ct, double fadeTime = 0) {
            if (colors == null) {
                LogUtil.Write("Error with color array!", "ERROR");
                return;
            }

            if (entLayer != null) {
                var lightMappings = bd.Lights;
                // Loop through lights in entertainment layer
                //LogUtil.Write(@"Sending to bridge...");
                foreach (var entLight in entLayer) {
                    // Get data for our light from map
                    var lightData = lightMappings.SingleOrDefault(item =>
                        item.Id == entLight.Id.ToString(CultureInfo.InvariantCulture));
                    // Return if not mapped
                    if (lightData == null) continue;
                    // Otherwise, get the corresponding sector color
                    var colorInt = lightData.TargetSector - 1;
                    var color = colors[colorInt];
                    // Make it into a color
                    var endColor = ClampBrightness(color, lightData, brightness);
                    //var xyColor = HueColorConverter.RgbToXY(endColor, CIE1931Gamut.PhilipsWideGamut);
                    //endColor = HueColorConverter.XYToRgb(xyColor, GetLightGamut(lightData.ModelId));
                    // If we're currently using a scene, animate it
                    if (Math.Abs(fadeTime) > 0.00001) {
                        // Our start color is the last color we had}
                        entLight.SetState(ct, endColor, endColor.GetBrightness(),
                            TimeSpan.FromSeconds(fadeTime));
                    } else {
                        // Otherwise, if we're streaming, just set the color
                        entLight.SetState(ct, endColor, endColor.GetBrightness());
                    }
                }
            } else {
                LogUtil.Write($@"Hue: Unable to fetch entertainment layer. {BridgeIp}");
            }
        }

        private static RGBColor ClampBrightness(Color colorIn, LightData lightData, int brightness) {
            var oColor = new RGBColor(colorIn.R, colorIn.G, colorIn.B);
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


        private async Task<List<Group>> ListGroups() {
            var all = await client.LocalHueClient.GetEntertainmentGroups().ConfigureAwait(true);
            var output = new List<Group>();
            output.AddRange(all);
            LogUtil.Write("Listed");
            return output;
        }


        public static async Task<RegisterEntertainmentResult> CheckAuth(string bridgeIp) {
            try {
                ILocalHueClient client = new LocalHueClient(bridgeIp);
                //Make sure the user has pressed the button on the bridge before calling RegisterAsync
                //It will throw an LinkButtonNotPressedException if the user did not press the button
                var result = await client.RegisterAsync("HueDream", Environment.MachineName, true)
                    .ConfigureAwait(false);
                LogUtil.Write($@"Hue: User name is {result.Username}.");
                return result;
            } catch (HueException) {
                LogUtil.Write($@"Hue: The link button is not pressed at {bridgeIp}.");
            }

            return null;
        }

        public static List<BridgeData> GetBridgeData() {
            var bridges = DreamData.GetItem<List<BridgeData>>("bridges");
            var newBridges = FindBridges();
            var output = new List<BridgeData>();
            if (bridges.Count > 0) {
                foreach (var b in bridges) {
                    if (b.Key != null && b.User != null) {
                        var hb = new HueBridge(b);
                        b.SetLights(hb.GetLights());
                        b.SetGroups(hb.ListGroups().Result);
                        hb.Dispose();
                    }

                    output.Add(b);
                }
            }

            foreach (var bb in newBridges) {
                var exists = false;
                foreach (var b in bridges) {
                    if (bb.Id == b.Id) exists = true;
                }

                if (exists) continue;
                output.Add(bb);
            }

            return output;
        }

        public static BridgeData[] FindBridges(int time = 2) {
            LogUtil.Write("Discovery Started.");
            IBridgeLocator locator = new MdnsBridgeLocator();
            var res = locator.LocateBridgesAsync(TimeSpan.FromSeconds(time)).Result;
            var output = res.Select(b => new BridgeData(b.IpAddress, b.BridgeId)).ToList();
            LogUtil.Write($"Discovery complete, found {output.Count} devices.");
            return output.ToArray();
        }

        private List<LightData> GetLights() {
            // If we have no IP or we're not authorized, return
            if (BridgeIp == "0.0.0.0" || BridgeUser == null || BridgeKey == null) return new List<LightData>();
            // Create client
            client.LocalHueClient.Initialize(BridgeUser);
            // Get lights
            var lights = bd.Lights ?? new List<LightData>();
            var res = client.LocalHueClient.GetLightsAsync().Result;
            var ld = res.Select(r => new LightData(r)).ToList();
            var output = new List<LightData>();
            foreach (var light in ld) {
                var add = true;
                foreach (var unused in lights.Where(oLight => oLight.Id == light.Id)) add = false;
                if (add) output.Add(light);
            }

            lights.AddRange(output);
            return lights;
        }


        public void Dispose() {
            Dispose(true);
        }


        private void Dispose(bool disposing) {
            if (disposed) {
                return;
            }

            if (disposing) {
                if (streaming) {
                    DisableStreaming();
                }

                client?.Dispose();
            }

            disposed = true;
        }
    }
}