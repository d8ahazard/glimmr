using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using HueDream.Models.Util;
using Q42.HueApi.ColorConverters;
using Q42.HueApi.ColorConverters.HSB;
using Q42.HueApi.Streaming;
using Q42.HueApi.Streaming.Extensions;
using Q42.HueApi.Streaming.Models;

namespace HueDream.Models.StreamingDevice.Hue {
    public sealed class HueBridge : IStreamingDevice, IDisposable {
        private BridgeData bd;
        private EntertainmentLayer entLayer;
        private StreamingHueClient client;
        private bool disposed;
        public int MaxBrightness { get; set; }
        public string Id { get; set; }
        
        public HueBridge(BridgeData data) {
            bd = data ?? throw new ArgumentNullException(nameof(data));
            BridgeIp = bd.IpAddress;
            client = StreamingSetup.GetClient(bd);
            disposed = false;
            Streaming = false;
            entLayer = null;
            MaxBrightness = data.MaxBrightness;
            LogUtil.Write(@"Hue: Loading bridge: " + BridgeIp);
        }

        private string BridgeIp { get; set; }


        public bool Streaming { get; set; }

        /// <summary>
        ///     Set up and create a new streaming layer based on our light map
        /// </summary>
        /// <param name="ct">A cancellation token.</param>
        public async void StartStream(CancellationToken ct) {
            StopStream();
            if (ct == null) throw new ArgumentException("Invalid cancellation token.");
            // Get our light map and filter for mapped lights
            LogUtil.Write($@"Hue: Connecting to bridge at {BridgeIp}...");
            // Grab our stream
            if (bd.Id == null || bd.Key == null || bd.Lights == null || bd.Groups == null) {
                LogUtil.Write("Bridge is not authorized.");
                return;
            }
            var stream = await StreamingSetup.SetupAndReturnGroup(client, bd, ct);
            // This is what we actually need
            if (stream == null) {
                LogUtil.Write("Error fetching bridge stream.","WARN");
                return;
            }
            entLayer = stream.GetNewLayer(true);
            LogUtil.WriteInc($"Starting Hue Stream: {BridgeIp}");
            while (!ct.IsCancellationRequested) {
                Streaming = true;
            }

            LogUtil.Write("Token canceled, self-excising.");
            StopStream();
        }

        public void StopStream() {
            var _ = StreamingSetup.StopStream(client, bd);
            LogUtil.WriteDec($"Stopping Hue Stream: {BridgeIp}");
            Streaming = false;
        }

        public void ReloadData() {
            var newData = DataUtil.GetCollectionItem<BridgeData>("bridges", Id);
            bd = newData;
            BridgeIp = bd.IpAddress;
            MaxBrightness = newData.MaxBrightness;
            LogUtil.Write(@"Hue: Reloaded bridge: " + BridgeIp);
        }

        /// <summary>
        ///     Update lights in entertainment layer
        /// </summary>
        /// <param name="colors">An array of 12 colors corresponding to sector data</param>
        /// <param name="fadeTime">Optional: how long to fade to next state</param>
        public void SetColor(List<Color> colors, double fadeTime = 0) {
            if (!Streaming) return;
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
                    var mb = lightData.OverrideBrightness ? lightData.Brightness : MaxBrightness;
                    if (mb < 100) {
                        color = ColorTransformUtil.ClampBrightness(color, mb); 
                    }
                    var oColor = new RGBColor(color.R, color.G, color.B);

                    // If we're currently using a scene, animate it
                    if (Math.Abs(fadeTime) > 0.00001) {
                        // Our start color is the last color we had}
                        entLight.SetState(CancellationToken.None, oColor, oColor.GetBrightness(),
                            TimeSpan.FromSeconds(fadeTime));
                    } else {
                        // Otherwise, if we're streaming, just set the color
                        entLight.SetState(CancellationToken.None, oColor, oColor.GetBrightness());
                    }
                }
            } else {
                LogUtil.Write($@"Hue: Unable to fetch entertainment layer. {BridgeIp}");
            }
        }


        public void Dispose() {
            Dispose(true);
        }


        private void Dispose(bool disposing) {
            if (disposed) {
                return;
            }

            if (disposing) {
                if (Streaming) {
                    StopStream();
                }

                client?.Dispose();
            }

            disposed = true;
        }
    }
}