using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Glimmr.Models.StreamingDevice.Yeelight;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Q42.HueApi;
using Q42.HueApi.ColorConverters;
using Q42.HueApi.ColorConverters.HSB;
using Q42.HueApi.Models.Groups;
using Q42.HueApi.Streaming;
using Q42.HueApi.Streaming.Extensions;
using Q42.HueApi.Streaming.Models;
using Serilog;

namespace Glimmr.Models.StreamingDevice.Hue {
	public sealed class HueDevice : IStreamingDevice, IDisposable {
		public bool Enable { get; set; }
		StreamingData IStreamingDevice.Data {
			get => Data;
			set => Data = (HueData) value;
		}

		private HueData Data { get; set; }
		private EntertainmentLayer _entLayer;
		private StreamingHueClient _client;
		private bool _disposed;
		private CancellationToken _ct;
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }
		public bool Streaming { get; set; }
		private ColorService _cs;
		private StreamingGroup _stream;

		private Task _updateTask;
		

		public HueDevice(HueData data, ColorService colorService = null) {
			
			DataUtil.GetItem<int>("captureMode");
			if (colorService != null) {
				_cs = colorService;
				_cs.ColorSendEvent += SetColor;
			} 
			Data = data ?? throw new ArgumentNullException(nameof(data));
			IpAddress = Data.IpAddress;
			_disposed = false;
			Streaming = false;
			_entLayer = null;
			Id = IpAddress;
			Tag = data.Tag;
			Brightness = data.Brightness;
			// Don't grab streaming group unless we need it
			if (Data?.User == null || Data?.Key == null || _client != null || colorService == null) return;
			_client = new StreamingHueClient(Data.IpAddress, Data.User, Data.Key);
			try {
				_stream = SetupAndReturnGroup().Result;
				Log.Debug("Stream is set.");
			} catch (Exception e) {
				Log.Warning("Exception: " + e.Message);
			}
		}

        

        
		public bool IsEnabled() {
			return Data.Enable;
		}

		/// <summary>
		///     Set up and create a new streaming layer based on our light map
		/// </summary>
		/// <param name="ct">A cancellation token.</param>
		public void StartStream(CancellationToken ct) {
			// Leave if not enabled
			if (!Data.Enable) return;
			
			// Leave if we have no client (not authorized)
			if (_client == null) {
				Log.Warning("We have not streaming client, can't start.");
				return;
			}
			
			// Leave if we have no mapped lights
			if (Data.MappedLights.Count == 0) {
				Log.Warning("Bridge has no mapped lights, returning.");
				return;
			}

			if (Streaming) {
				Log.Debug("We are already streaming.");
				return;
			}
			
			if (ct == null) throw new ArgumentException("Invalid cancellation token.");
			_ct = ct;
			
			// This is what we actually need
			if (_stream == null) {
				Log.Warning("Error fetching bridge stream.");
				Streaming = false;
				return;
			}
			
			//Connect to the streaming group
			try {
				var groupId = Data.SelectedGroup;
				var group = _client.LocalHueClient.GetGroupAsync(groupId).Result;
				if (group == null) {
					Log.Warning("Unable to fetch group with ID of " + groupId);
					return;
				}

				_client.Connect(group.Id);
				Log.Debug("Connected.");
			} catch (Exception e) {
				Log.Debug("Streaming exception caught: " + e.Message);
			}

			_updateTask = _client.AutoUpdate(_stream, _ct);
			Log.Debug("Group setup complete, returning.");

			Log.Debug("Getting entertainment layer.");
			_entLayer = _stream.GetNewLayer(true);
			Log.Debug($"Hue: Stream started: {IpAddress}");
			Streaming = true;
		}

		public void StopStream() {
			Log.Debug($"Hue: Stopping Stream: {IpAddress}...");
			var _ = StopStream(_client, Data);
			if (Streaming) ResetColors();
			Streaming = false;
			Log.Debug("Hue: Streaming Stopped.");
		}

		
		private void ResetColors() {
			foreach (var entLight in _entLayer) {
				// Get data for our light from map
				var lightMappings = Data.Lights;
				var lightData = lightMappings.SingleOrDefault(item =>
					item.Id == entLight.Id.ToString(CultureInfo.InvariantCulture));
				if (lightData == null) continue;
				// var sat = lightData.LastState.Saturation;
				// var bri = lightData.LastState.Brightness;
				// var hue = lightData.LastState.Hue;
				// var isOn = lightData.LastState.On;
				// var ll = new List<string> {lightData.Id};
				// var cmd = new LightCommand {Saturation = sat, Brightness = bri, Hue = hue, On = isOn};
				// _client.LocalHueClient.SendCommandAsync(cmd, ll);
			}
		}

		public void ReloadData() {
			var newData = (HueData) DataUtil.GetCollectionItem<HueData>("Dev_Hue", Id);
			DataUtil.GetItem<int>("captureMode");
			Data = newData;
			IpAddress = Data.IpAddress;
			Brightness = newData.Brightness;
			Log.Debug(@"Hue: Reloaded bridge: " + IpAddress);
		}

		/// <summary>
		///     Update lights in entertainment layer
		/// </summary>
		/// <param name="_">LED colors, we don't need this.</param>
		/// <param name="colors">Sector colors.</param>
		/// <param name="fadeTime">Optional: how long to fade to next state</param>
		public void SetColor(List<Color> _, List<Color> colors, double fadeTime = 0) {
			if (!Streaming || !Data.Enable) {
				return;
			}
			if (colors == null) {
				return;
			}

			if (_entLayer == null) return;
			var lightMappings = Data.MappedLights;
			foreach (var entLight in _entLayer) {
				// Get data for our light from map
				var lightData = lightMappings.SingleOrDefault(item =>
					item.Id == entLight.Id.ToString());
				// Return if not mapped
				if (lightData == null) continue;
				// Otherwise, get the corresponding sector color
				var tSector = lightData.TargetSector;
				var colorInt = tSector - 1;
				var color = colors[colorInt];
				var mb = lightData.Override ? lightData.Brightness : Brightness;
				var oColor = new RGBColor(color.R, color.G, color.B);
				// If we're currently using a scene, animate it
				if (Math.Abs(fadeTime) > 0.01) {
					// Our start color is the last color we had}
					entLight.SetState(_ct, oColor, mb,
						TimeSpan.FromMilliseconds(fadeTime));
				} else {
					// Otherwise, if we're streaming, just set the color
					entLight.SetState(_ct, oColor, mb);
				}
			}
			
		}

		
        
		public async Task<HueData> RefreshData() {
			// If we have no IP or we're not authorized, return
			var newLights = new List<LightData>();
			var newGroups = new List<HueGroup>();

			if (Data.IpAddress == "0.0.0.0" || Data.User == null || Data.Key == null) {
				Data.Lights = newLights;
				Data.Groups = newGroups;
				Log.Debug("No authorization, returning empty lights.");
				return Data;
			}
			// Get our client
			_client.LocalHueClient.Initialize(Data.User);
			// Get lights
			try {
				var res = _client.LocalHueClient.GetLightsAsync().Result;
				Data.Lights = res.Select(r => new LightData(r)).ToList();
				var all = await _client.LocalHueClient.GetEntertainmentGroups();
				var config = new MapperConfiguration(cfg => cfg.CreateMap<Group, HueGroup>());
				var mapper = new Mapper(config);
				newGroups.AddRange(all.Select(g => mapper.Map<HueGroup>(g)));
				Log.Debug("Groups: " + JsonConvert.SerializeObject(newGroups));
				Data.Groups = newGroups;
			} catch (Exception d) {
				Log.Debug("Caught an exception: " + d.Message);    
			}

			return Data;
		}


		private static async Task StopStream(StreamingHueClient client, HueData b) {
            if (client == null || b == null) {
                throw new ArgumentException("Invalid argument.");
            }

            var id = b.SelectedGroup;
            await client.LocalHueClient.SetStreamingAsync(id, false).ConfigureAwait(true);
        }

       private async Task<StreamingGroup> SetupAndReturnGroup() {
	       var groupId = Data.SelectedGroup;

            if (_client == null || Data == null || groupId == null) {
                Log.Warning("Client or data or groupId are null, returning...");
                return null;
            }
            
            try {
	            //Get the entertainment group
                Log.Debug("Grabbing ent group...");
                var group = _client.LocalHueClient.GetGroupAsync(groupId).Result;
                if (group == null) {
                    Log.Warning("Unable to fetch group with ID of " + groupId);
                    return null;
                }
                
                var lights = group.Lights;
                var mappedLights = new List<string>();

                foreach (var ml in lights.SelectMany(light => Data.MappedLights.Where(ml => ml.Id.ToString() == light))) {
	                if (ml.TargetSector != -1) {
		                mappedLights.Add(ml.Id);
	                }
                }

                if (mappedLights.Count == 0) {
                    Log.Debug("No mapped lights, nothing to do.");
                    return null;
                }
                
                var stream = new StreamingGroup(mappedLights);
                
                return stream;
            
            } catch (SocketException e) {
                Log.Debug("Socket exception occurred, can't return group right now: " + e.Message);
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