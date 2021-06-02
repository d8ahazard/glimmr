using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Q42.HueApi;
using Q42.HueApi.ColorConverters;
using Q42.HueApi.Models.Groups;
using Q42.HueApi.Streaming;
using Q42.HueApi.Streaming.Extensions;
using Q42.HueApi.Streaming.Models;
using Serilog;

namespace Glimmr.Models.ColorTarget.Hue {
	public sealed class HueDevice : ColorTarget, IColorTarget, IDisposable {
		private HueData Data { get; set; }
		public bool Enable { get; set; }

		IColorTargetData IColorTarget.Data {
			get => Data;
			set => Data = (HueData) value;
		}

		public bool Testing { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }
		public bool Streaming { get; set; }

		private string _selectedGroup;

		private List<LightData> _lights;

		private readonly StreamingHueClient _client;
		private readonly StreamingGroup _stream;
		private CancellationToken _ct;
		private bool _disposed;
		private EntertainmentLayer _entLayer;
		private string _user;
		private string _token;
		private List<LightMap> _lightMappings;


		public HueDevice(HueData data, ColorService colorService) : base(colorService) {
			DataUtil.GetItem<int>("captureMode");
			colorService.ColorSendEvent += SetColor;
			Data = data;
			Id = Data.Id;
			_disposed = false;
			Streaming = false;
			_entLayer = null;
			SetData();
			// Don't grab streaming group unless we need it
			if (_user == null || _token == null || _client != null) {
				return;
			}

			Log.Debug($"Creating client: {IpAddress}, {_user}, {_token}");
			_client = new StreamingHueClient(IpAddress, _user, _token);
			try {
				_stream = SetupAndReturnGroup().Result;
			} catch (Exception e) {
				Log.Warning("Exception: " + e.Message + " at " + e.StackTrace);
			}
		}


		public HueDevice(HueData data) {
			DataUtil.GetItem<int>("captureMode");
			Data = data;
			SetData();
			_disposed = false;
			Streaming = false;
			_entLayer = null;
			
			// Don't grab streaming group unless we need it
			if (Data?.User == null || Data?.Token == null || _client != null) {
				return;
			}

			_client = new StreamingHueClient(IpAddress, _user, _token);
			try {
				_stream = SetupAndReturnGroup().Result;
				Log.Debug("Stream is set.");
			} catch (Exception e) {
				Log.Warning("Exception: " + e.Message);
			}
		}

		


		public Task FlashColor(Color color) {
			if (!Enable) {
				return Task.CompletedTask;
			}

			if (_entLayer == null) {
				return Task.CompletedTask;
			}

			foreach (var entLight in _entLayer) {
				// Get data for our light from map
				var oColor = new RGBColor(color.R, color.G, color.B);
				// If we're currently using a scene, animate it
				entLight.SetState(_ct, oColor, 255);
			}

			return Task.CompletedTask;
		}

		public bool IsEnabled() {
			return Enable;
		}

		/// <summary>
		///     Set up and create a new streaming layer based on our light map
		/// </summary>
		/// <param name="ct">A cancellation token.</param>
		public async Task StartStream(CancellationToken ct) {
			// Leave if not enabled
			if (!Enable) {
				return;
			}

			Log.Information($"{Data.Tag}::Starting stream: {Data.Id}...");
			// Leave if we have no client (not authorized)
			if (_client == null) {
				Log.Warning("We have no streaming client, can't start.");
				return;
			}

			// Leave if we have no mapped lights
			if (_lightMappings.Count == 0) {
				Log.Warning("Bridge has no mapped lights, returning.");
				return;
			}

			if (Streaming) {
				Log.Debug("We are already streaming.");
				return;
			}

			_ct = ct;

			// This is what we actually need
			if (_stream == null) {
				Log.Warning("Error fetching bridge stream.");
				Streaming = false;
				return;
			}

			//Connect to the streaming group
			try {
				var group = await _client.LocalHueClient.GetGroupAsync(_selectedGroup);
				if (group == null) {
					Log.Warning("Unable to fetch group with ID of " + _selectedGroup);
					return;
				}

				await _client.Connect(group.Id);
				Log.Debug("Connected.");
			}catch (SocketException s) {
				if (s.Message.Contains("already connected")) {
					Log.Debug("Client is already connected.");
				} else {
					return;
				}
			} catch (Exception e) {
				Log.Debug("Streaming exception caught: " + e.Message + " at " + e.StackTrace);
				return;
			}

			Log.Debug("Setting autoUpdate...");
			_client.AutoUpdate(_stream, _ct).ConfigureAwait(false);
			_entLayer = _stream.GetNewLayer(true);
			Log.Information($"{Data.Tag}::Stream started: {Data.Id}");
			Streaming = true;
		}

		

		public Task ReloadData() {
			Log.Debug("Reloading Hue data...");
			Data = DataUtil.GetDevice<HueData>(Id);
			SetData();
			return Task.CompletedTask;
		}

		/// <summary>
		///     Update lites in entertainment group...
		/// </summary>
		/// <param name="list"></param>
		/// <param name="sectors"></param>
		/// <param name="fadeTime"></param>
		/// <param name="force"></param>
		public void SetColor(List<Color> list, List<Color> sectors, int fadeTime, bool force = false) {
			if (!Streaming || !Enable || _entLayer == null || Testing && !force) {
				return;
			}

			
			foreach (var entLight in _entLayer) {
				// Get data for our light from map
				var lightData = _lightMappings.SingleOrDefault(item =>
					item.Id == entLight.Id.ToString());
				// Return if not mapped
				if (lightData == null) {
					continue;
				}

				// Otherwise, get the corresponding sector color
				var tSector = lightData.TargetSector;
				var target = tSector - 1;
				target = ColorUtil.CheckDsSectors(target);
				if (target >= sectors.Count) {
					continue;
				}

				var color = sectors[target];
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

			ColorService.Counter.Tick(Id);
		}


		public void Dispose() {
			Dispose(true);
		}

		public void FlashLight(Color color, int lightId) {
			if (!Enable) {
				return;
			}

			if (_entLayer == null) {
				return;
			}

			foreach (var entLight in _entLayer) {
				if (lightId == entLight.Id) {
					// Get data for our light from map
					var oColor = new RGBColor(color.R, color.G, color.B);
					// If we're currently using a scene, animate it
					entLight.SetState(_ct, oColor, 255);
				}
			}
		}


		private async Task ResetColors() {
			foreach (var entLight in _entLayer) {
				// Get data for our light from map
				
				var lightData = _lights.SingleOrDefault(item =>
					item.Id == entLight.Id.ToString(CultureInfo.InvariantCulture));
				if (lightData == null) {
					continue;
				}

				var sat = lightData.LastState.Saturation;
				var bri = lightData.LastState.Brightness;
				var hue = lightData.LastState.Hue;
				var isOn = lightData.LastState.On;
				var ll = new List<string> {lightData.Id};
				var cmd = new LightCommand {Saturation = sat, Brightness = bri, Hue = hue, On = isOn};
				await _client.LocalHueClient.SendCommandAsync(cmd, ll);
			}
		}

		private void SetData() {
			IpAddress = Data.IpAddress;
			Tag = Data.Tag;
			_user = Data.User;
			_token = Data.Token;
			Enable = Data.Enable;
			Brightness = Data.Brightness;
			_lights = Data.Lights;
			_lightMappings = Data.MappedLights;
			Enable = Data.Enable;
			_selectedGroup = Data.SelectedGroup;
		}

		
		public async Task StopStream() {
			if (!Enable) {
				return;
			}


			try {
				await _client.LocalHueClient.SetStreamingAsync(_selectedGroup, false);
			} catch (Exception) {
				// ignored
			}

			Streaming = false;
		}

		private async Task<StreamingGroup> SetupAndReturnGroup() {

			if (_client == null || Data == null || _selectedGroup == null) {
				Log.Warning("Client or data or groupId are null, returning...");
				return null;
			}

			try {
				//Get the entertainment group
				var group = await _client.LocalHueClient.GetGroupAsync(_selectedGroup);
				if (group == null) {
					Log.Warning("Unable to fetch group with ID of " + _selectedGroup);
					return null;
				}

				var lights = group.Lights;
				var mappedLights = new List<string>();

				foreach (var ml in lights.SelectMany(light => _lightMappings.Where(ml => ml.Id.ToString() == light))
				) {
					if (ml.TargetSector != -1) {
						mappedLights.Add(ml.Id);
					}
				}

				if (mappedLights.Count == 0) {
					return null;
				}

				var stream = new StreamingGroup(mappedLights);

				return stream;
			} catch (SocketException e) {
				Log.Debug("Socket exception occurred, can't return group right now: " + e.Message);
			}

			return null;
		}


		private void Dispose(bool disposing) {
			if (_disposed) {
				return;
			}

			if (disposing) {
				_client.Dispose();
			}

			_disposed = true;
		}
	}
}