using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Q42.HueApi;
using Q42.HueApi.ColorConverters;
using Q42.HueApi.ColorConverters.HSB;
using Q42.HueApi.Models.Groups;
using Q42.HueApi.Streaming;
using Q42.HueApi.Streaming.Extensions;
using Q42.HueApi.Streaming.Models;

namespace Glimmr.Models.StreamingDevice.Hue {
	public sealed class HueBridge : IStreamingDevice, IDisposable {
		public bool Enable { get; set; }
		StreamingData IStreamingDevice.Data {
			get => Data;
			set => Data = (BridgeData) value;
		}

		private BridgeData Data { get; set; }
		private EntertainmentLayer _entLayer;
		private StreamingHueClient _client;
		private bool _disposed;
		private int _captureMode;
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }

		public bool Streaming { get; set; }


		public HueBridge(BridgeData data) {
			_captureMode = DataUtil.GetItem<int>("captureMode");
			Data = data ?? throw new ArgumentNullException(nameof(data));
			IpAddress = Data.IpAddress;
			_disposed = false;
			Streaming = false;
			_entLayer = null;
			Id = data.Id;
			Tag = data.Tag;
			Brightness = data.Brightness;
		}

        

        
		public bool IsEnabled() {
			return Data.Enable;
		}

		/// <summary>
		///     Set up and create a new streaming layer based on our light map
		/// </summary>
		/// <param name="ct">A cancellation token.</param>
		public async void StartStream(CancellationToken ct) {
			if (Data.Id == null || Data.Key == null || Data.Lights == null || Data.Groups == null) {
				LogUtil.Write("Bridge is not authorized.","WARN");
				return;
			}

			if (!Data.Enable) return;
            
			LogUtil.Write("Hue: Starting stream...");
			SetClient();
			try {
				// Make sure we are not already streaming.
				var _ = StreamingSetup.StopStream(_client, Data);
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
			DataUtil.InsertCollection<BridgeData>("Dev_Hue", Data);
			StreamingGroup stream;
			try {
				stream = StreamingSetup.SetupAndReturnGroup(_client, Data, ct).Result;
			} catch (Exception e) {
				LogUtil.Write("SException (Probably tried stopping/starting too quickly): " + e.Message, "WARN");
				return;
			}
            

			// This is what we actually need
			if (stream == null) {
				LogUtil.Write("Error fetching bridge stream.", "WARN");
				Streaming = false;
				return;
			}

			_entLayer = stream.GetNewLayer(true);
			LogUtil.Write($"Hue: Stream started: {IpAddress}");
			Streaming = true;
		}

		public void StopStream() {
			LogUtil.Write($"Hue: Stopping Stream: {IpAddress}...");
			var _ = StreamingSetup.StopStream(_client, Data);
			if (Streaming) ResetColors();
			Streaming = false;
			LogUtil.Write("Hue: Streaming Stopped.");
		}

		private void SetClient() {
			if (Data?.User == null || Data?.Key == null || _client != null) return;
			_client = new StreamingHueClient(Data.IpAddress, Data.User, Data.Key);
		}

		private void ResetColors() {
			foreach (var entLight in _entLayer) {
				// Get data for our light from map
				var lightMappings = Data.Lights;
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
			var newData = (BridgeData) DataUtil.GetCollectionItem<BridgeData>("Dev_Hue", Id);
			_captureMode = DataUtil.GetItem<int>("captureMode");
			Data = newData;
			IpAddress = Data.IpAddress;
			Brightness = newData.Brightness;
			LogUtil.Write(@"Hue: Reloaded bridge: " + IpAddress);
		}

		/// <summary>
		///     Update lights in entertainment layer
		/// </summary>
		/// <param name="colors">An array of 12 colors corresponding to sector data</param>
		/// <param name="fadeTime">Optional: how long to fade to next state</param>
		public void SetColor(List<Color> colors, double fadeTime = 0) {
			
			if (!Streaming) {
				LogUtil.Write("Hue is not streaming.");
				return;
			}
			if (colors == null) {
				LogUtil.Write("Error with color array!", "ERROR");
				return;
			}

			if (_entLayer != null) {
				var lightMappings = Data.Lights;
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

			if (Data.IpAddress == "0.0.0.0" || Data.User == null || Data.Key == null) {
				Data.Lights = newLights;
				Data.Groups = newGroups;
				LogUtil.Write("No authorization, returning empty lights.");
				return;
			}
			// Get our client
			SetClient();
			_client.LocalHueClient.Initialize(Data.User);
			// Get lights
			var lights = Data.Lights ?? new List<LightData>();
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

				Data.Lights = newLights;
				var all = await _client.LocalHueClient.GetEntertainmentGroups();
				newGroups.AddRange(all);
				Data.Groups = newGroups;
			} catch (AggregateException e) {
				LogUtil.Write("Aggregate exception: " + e);
			} catch (HttpRequestException f) {
				LogUtil.Write("Http Request Exception: " + f);
			} catch (SocketException g) {
				LogUtil.Write("Socket Exception: " + g);
			}
		}
        
		public async Task<BridgeData> RefreshData(int timeout=5) {
			// If we have no IP or we're not authorized, return
			var newLights = new List<LightData>();
			var newGroups = new List<Group>();

			if (Data.IpAddress == "0.0.0.0" || Data.User == null || Data.Key == null) {
				Data.Lights = newLights;
				Data.Groups = newGroups;
				LogUtil.Write("No authorization, returning empty lights.");
				return Data;
			}
			// Get our client
			SetClient();
			_client.LocalHueClient.Initialize(Data.User);
			// Get lights
			var lights = Data.Lights ?? new List<LightData>();
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

				Data.Lights = newLights;
				var all = await _client.LocalHueClient.GetEntertainmentGroups();
				newGroups.AddRange(all);
				Data.Groups = newGroups;
			} catch (Exception d) {
				LogUtil.Write("Caught an exception: " + d.Message);    
			}

			return Data;
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