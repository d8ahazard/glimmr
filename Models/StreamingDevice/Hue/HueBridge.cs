using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HueDream.Models.Util;
using ISocketLite.PCL.Exceptions;
using Newtonsoft.Json;
using Q42.HueApi;
using Q42.HueApi.ColorConverters;
using Q42.HueApi.ColorConverters.HSB;
using Q42.HueApi.Interfaces;
using Q42.HueApi.Models.Bridge;
using Q42.HueApi.Models.Groups;
using Q42.HueApi.Streaming;
using Q42.HueApi.Streaming.Extensions;
using Q42.HueApi.Streaming.Models;

namespace HueDream.Models.StreamingDevice.Hue {
    public sealed class HueBridge : IStreamingDevice, IDisposable {
        public BridgeData Bd;
        private EntertainmentLayer _entLayer;
        private StreamingHueClient _client;
        private bool _disposed;
        private int _captureMode;
        public int Brightness { get; set; }
        public string Id { get; set; }
        public string IpAddress { get; set; }

        public HueBridge(BridgeData data) {
            _captureMode = DataUtil.GetItem<int>("captureMode");
            Bd = data ?? throw new ArgumentNullException(nameof(data));
            IpAddress = Bd.IpAddress;
            _disposed = false;
            Streaming = false;
            _entLayer = null;
            Brightness = data.Brightness;
        }

        

        public bool Streaming { get; set; }

        /// <summary>
        ///     Set up and create a new streaming layer based on our light map
        /// </summary>
        /// <param name="ct">A cancellation token.</param>
        public async void StartStream(CancellationToken ct) {
            if (Bd.Id == null || Bd.Key == null || Bd.Lights == null || Bd.Groups == null) {
                LogUtil.Write("Bridge is not authorized.","WARN");
                return;
            }
            LogUtil.Write("Hue: Starting stream...");
            SetClient();
			try {
                // Make sure we are not already streaming.
                var _ = StreamingSetup.StopStream(_client, Bd);
                if (Streaming) ResetColors();
                Streaming = false;
			} catch (SocketException e) {
				LogUtil.Write("Socket exception, probably our bridge wasn't streaming. Oh well: " + e.Message);
			}
            if (ct == null) throw new ArgumentException("Invalid cancellation token.");
            // Get our light map and filter for mapped lights
            // Grab our stream
            
            // Save previous light state(s) before stopping
            RefreshData();
            DataUtil.InsertCollection<BridgeData>("bridges", Bd);
            StreamingGroup stream;
            try {
                stream = await StreamingSetup.SetupAndReturnGroup(_client, Bd, ct);
            } catch (Exception e) {
                LogUtil.Write("SException (Probably tried stopping/starting too quickly): " + e.Message, "WARN");
                return;
            }
            

            // This is what we actually need
            if (stream == null) {
                LogUtil.Write("Error fetching bridge stream.", "WARN");
                return;
            }

            _entLayer = stream.GetNewLayer(true);
            LogUtil.Write($"Hue: Stream started: {IpAddress}");
            while (!ct.IsCancellationRequested) {
                Streaming = true;
            }

            StopStream();
        }

        public void StopStream() {
            LogUtil.Write($"Hue: Stopping Stream: {IpAddress}...");
            var _ = StreamingSetup.StopStream(_client, Bd);
            if (Streaming) ResetColors();
            Streaming = false;
            LogUtil.Write("Hue: Streaming Stopped.");
        }

        private void SetClient() {
            if (Bd?.User == null || Bd?.Key == null || _client != null) return;
            _client = new StreamingHueClient(Bd.IpAddress, Bd.User, Bd.Key);
        }

        private void ResetColors() {
            foreach (var entLight in _entLayer) {
                // Get data for our light from map
                var lightMappings = Bd.Lights;
                var lightData = lightMappings.SingleOrDefault(item =>
                    item.Id == entLight.Id.ToString(CultureInfo.InvariantCulture));
                if (lightData == null) continue;
                var sat = lightData.LastState.Saturation;
                var bri = lightData.LastState.Brightness;
                var hue = lightData.LastState.Hue;
                var isOn = lightData.LastState.On;
                var ll = new List<string> {lightData.Id};
                var cmd = new LightCommand {Saturation = sat, Brightness = bri, Hue = hue, On = isOn};
                _client.LocalHueClient.SendCommandAsync(cmd, ll);
            }
        }

        public void ReloadData() {
            var newData = DataUtil.GetCollectionItem<BridgeData>("bridges", Id);
            _captureMode = DataUtil.GetItem<int>("captureMode");
            Bd = newData;
            IpAddress = Bd.IpAddress;
            Brightness = newData.MaxBrightness;
            LogUtil.Write(@"Hue: Reloaded bridge: " + IpAddress);
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

            if (_entLayer != null) {
                var lightMappings = Bd.Lights;
                // Loop through lights in entertainment layer
                //LogUtil.Write(@"Sending to bridge...");
                foreach (var entLight in _entLayer) {
                    // Get data for our light from map
                    var lightData = lightMappings.SingleOrDefault(item =>
                        item.Id == entLight.Id.ToString(CultureInfo.InvariantCulture));
                    // Return if not mapped
                    if (lightData == null) continue;
                    // Otherwise, get the corresponding sector color
                    var tSector = _captureMode == 0 ? lightData.TargetSector : lightData.TargetSectorV2;
                    var colorInt = tSector - 1;
                    var color = colors[colorInt];
                    var mb = lightData.OverrideBrightness ? lightData.Brightness : Brightness;
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
                LogUtil.Write($@"Hue: Unable to fetch entertainment layer. {IpAddress}");
            }
        }

        
        public async void RefreshData() {
            // If we have no IP or we're not authorized, return
            var newLights = new List<LightData>();
            var newGroups = new List<Group>();

            if (Bd.IpAddress == "0.0.0.0" || Bd.User == null || Bd.Key == null) {
                Bd.Lights = newLights;
                Bd.Groups = newGroups;
                LogUtil.Write("No authorization, returning empty lights.");
                return;
            }
            // Get our client
            SetClient();
            _client.LocalHueClient.Initialize(Bd.User);
            // Get lights
            var lights = Bd.Lights ?? new List<LightData>();
            try {
                var res = _client.LocalHueClient.GetLightsAsync().Result;
                var ld = res.Select(r => new LightData(r)).ToList();

                foreach (var light in ld) {
                    foreach (var ex in lights.Where(ex => ex.Id == light.Id)) {
                        light.TargetSector = ex.TargetSector;
                        light.TargetSectorV2 = ex.TargetSectorV2;
                        if (light.TargetSectorV2 == -1 && light.TargetSector != -1) {
                            light.TargetSectorV2 = ex.TargetSector * 2;
                        }
                        light.Brightness = ex.Brightness;
                        light.OverrideBrightness = ex.OverrideBrightness;
                    }

                    newLights.Add(light);
                }

                Bd.Lights = newLights;
                var all = await _client.LocalHueClient.GetEntertainmentGroups();
                newGroups.AddRange(all);
                Bd.Groups = newGroups;
            } catch (AggregateException e) {
                LogUtil.Write("Aggregate exception: " + e);
            } catch (HttpRequestException f) {
                LogUtil.Write("Http Request Exception: " + f);
            } catch (System.Net.Sockets.SocketException g) {
                LogUtil.Write("Socket Exception: " + g);
            }
        }
        
       

       
        

        public void Dispose() {
            Dispose(true);
        }


        private void Dispose(bool disposing) {
            if (_disposed) {
                return;
            }

            if (disposing) {
                if (Streaming) {
                    StopStream();
                    _client.Dispose();
                }
            }

            _disposed = true;
        }
    }
}