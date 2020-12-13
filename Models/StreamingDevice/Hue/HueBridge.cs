using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Newtonsoft.Json;
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
			set => Data = (HueData) value;
		}

		private HueData Data { get; set; }
		private EntertainmentLayer _entLayer;
		private StreamingHueClient _client;
		private bool _disposed;
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }
		public bool Streaming { get; set; }


		public HueBridge(HueData data) {
			DataUtil.GetItem<int>("captureMode");
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
				var _ = StopStream(_client, Data);
				if (Streaming) ResetColors();
				Streaming = false;
			} catch (SocketException e) {
				LogUtil.Write("Socket exception, probably our bridge wasn't streaming. Oh well: " + e.Message);
			}
			if (ct == null) throw new ArgumentException("Invalid cancellation token.");
			// Get our light map and filter for mapped lights
			// Grab our stream
            
			// Save previous light state(s) before stopping
			await RefreshData();
			DataUtil.InsertCollection<HueData>("Dev_Hue", Data);
			StreamingGroup stream;
			try {
				stream = SetupAndReturnGroup(_client, Data, ct).Result;
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
			var _ = StopStream(_client, Data);
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
			var newData = (HueData) DataUtil.GetCollectionItem<HueData>("Dev_Hue", Id);
			DataUtil.GetItem<int>("captureMode");
			Data = newData;
			IpAddress = Data.IpAddress;
			Brightness = newData.Brightness;
			LogUtil.Write(@"Hue: Reloaded bridge: " + IpAddress);
		}

		/// <summary>
		///     Update lights in entertainment layer
		/// </summary>
		/// <param name="_">LED colors, we don't need this.</param>
		/// <param name="colors">Sector colors.</param>
		/// <param name="fadeTime">Optional: how long to fade to next state</param>
		public void SetColor(List<Color> _, List<Color> colors, double fadeTime = 0) {
			
			if (!Streaming) {
				LogUtil.Write("Hue is not streaming.");
				return;
			}
			if (colors == null) {
				LogUtil.Write("Error with color array!", "ERROR");
				return;
			}

			if (_entLayer != null) {
				var lightMappings = Data.MappedLights;
				foreach (var entLight in _entLayer) {
					// Get data for our light from map
					var lightData = lightMappings.SingleOrDefault(item =>
						item.Id == entLight.Id);
					// Return if not mapped
					if (lightData == null) continue;
					// Otherwise, get the corresponding sector color
					var tSector = lightData.TargetSector;
					var colorInt = tSector - 1;
					var color = colors[colorInt];
					var mb = lightData.Override ? lightData.Brightness : Brightness;
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

		
        
		public async Task<HueData> RefreshData() {
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
			try {
				var res = _client.LocalHueClient.GetLightsAsync().Result;
				Data.Lights = res.Select(r => new LightData(r)).ToList();
				var all = await _client.LocalHueClient.GetEntertainmentGroups();
				newGroups.AddRange(all);
				Data.Groups = newGroups;
			} catch (Exception d) {
				LogUtil.Write("Caught an exception: " + d.Message);    
			}

			return Data;
		}
       

       public static async Task StopStream(StreamingHueClient client, HueData b) {
            if (client == null || b == null) {
                throw new ArgumentException("Invalid argument.");
            }

            var id = b.SelectedGroup;
            await client.LocalHueClient.SetStreamingAsync(id, false).ConfigureAwait(true);
        }

        public static async Task<StreamingGroup> SetupAndReturnGroup(StreamingHueClient client, HueData b,
            CancellationToken ct) {
            
            if (client == null || b == null) {
                throw new ArgumentException("Invalid argument.");
            }
            try {
                var groupId = b.SelectedGroup;
                LogUtil.Write("HueStream: Selecting group ID: " + groupId);
                if (groupId == null && b.Groups != null && b.Groups.Count > 0) {
                    groupId = b.Groups[0].Id;
                }

                if (groupId == null) {
                    LogUtil.Write("HueStream: Group ID is null!");
                    return null;
                }
                //Get the entertainment group
                LogUtil.Write("Grabbing ent group...");
                var group = client.LocalHueClient.GetGroupAsync(groupId).Result;
                if (group == null) {
                    LogUtil.Write("Group is null, trying with first group ID.");
                    var groups = b.Groups;
                    if (groups.Count > 0) {
                        groupId = groups[0].Id;
                        group = client.LocalHueClient.GetGroupAsync(groupId).Result;
                        if (group != null) {
                            LogUtil.Write(@$"Selected first group: {groupId}");
                        } else {
                            LogUtil.Write(@"Unable to load group, can't connect for streaming.");
                            return null;
                        }
                    }
                } else {
                    LogUtil.Write("HueStream: Entertainment Group retrieved...");
                }

                //Create a streaming group
                if (group != null) {
                    var lights = group.Lights;
                    LogUtil.Write("HueStream: We have group, mapping lights: " + JsonConvert.SerializeObject(lights));
                    var mappedLights = new List<string>();
                    foreach (var light in lights) {
                        foreach (var ml in from ml in b.MappedLights where ml.Id.ToString() == light where ml.TargetSector != -1 where !mappedLights.Contains(light) select ml) {
                            LogUtil.Write("Adding mapped ID: " + ml.Id);
                            mappedLights.Add(light);
                        }
                    }

                    LogUtil.Write("Getting streaming group using ml: " + JsonConvert.SerializeObject(mappedLights));

                    if (mappedLights.Count == 0) {
                        LogUtil.Write("No mapped lights, nothing to do.");
                        return null;
                    }
                    
                    var stream = new StreamingGroup(mappedLights);
                    LogUtil.Write("Stream Got.");
                    //Connect to the streaming group
                    try {
                        LogUtil.Write("Connecting...");
                        await client.Connect(group.Id);
                        LogUtil.Write("Connected.");
                    } catch (Exception e) {
                        LogUtil.Write("Streaming exception caught: " + e.Message);
                    }

                    client.AutoUpdate(stream, ct);
                    LogUtil.Write("Group setup complete, returning.");
                    return stream;
                }

                LogUtil.Write("Uh, the group retrieved is null...");

            } catch (SocketException e) {
                LogUtil.Write("Socket exception occurred, can't return group right now: " + e.Message);
            }

            return null;
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