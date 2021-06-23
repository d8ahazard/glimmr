#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models.Util;
using Glimmr.Services;
using Q42.HueApi;
using Q42.HueApi.ColorConverters;
using Q42.HueApi.Streaming;
using Q42.HueApi.Streaming.Extensions;
using Q42.HueApi.Streaming.Models;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Hue {
	public sealed class HueDevice : ColorTarget, IColorTarget, IDisposable {
		private HueData Data { get; set; }

		private readonly StreamingHueClient _client;
		private readonly StreamingGroup _stream;
		private CancellationToken _ct;
		private bool _disposed;
		private EntertainmentLayer _entLayer;
		private List<LightMap> _lightMappings;

		private List<LightData> _lights;

		private string _selectedGroup;
		private Dictionary<string, int> _targets;
		private string _token;

		private Task _updateTask;
		private string _user;


		public HueDevice(HueData data, ColorService colorService) : base(colorService) {
			DataUtil.GetItem<int>("captureMode");
			colorService.ColorSendEvent += SetColor;
			colorService.ControlService.RefreshSystemEvent += SetData;
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
			} catch (SocketException s) {
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
			_updateTask = _client.AutoUpdate(_stream, _ct);
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
				var target = _targets[entLight.Id.ToString()];
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


		public async Task StopStream() {
			if (!Enable) {
				return;
			}

			Log.Information($"{Data.Tag}::Starting stream... {Data.Id}");

			try {
				_client.LocalHueClient.SetStreamingAsync(_selectedGroup, false).ConfigureAwait(false);
				if (!_updateTask.IsCompleted) {
					_updateTask.Dispose();
				}

				//_client.Close();
			} catch (Exception) {
				// ignored
			}

			Streaming = false;
			Log.Information($"{Data.Tag}::Stream stopped. {Data.Id}");
		}

		public bool IsEnabled() {
			return Enable;
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
			var sd = DataUtil.GetSystemData();
			_targets = new Dictionary<string, int>();
			IpAddress = Data.IpAddress;
			Tag = Data.Tag;
			_user = Data.User;
			_token = Data.Token;
			Enable = Data.Enable;
			Brightness = Data.Brightness;
			_lights = Data.Lights;
			_lightMappings = Data.MappedLights;
			foreach (var ld in _lightMappings) {
				var target = ld.TargetSector;
				if ((CaptureMode) sd.CaptureMode == CaptureMode.DreamScreen) {
					target = ColorUtil.CheckDsSectors(target);
				}

				if (sd.UseCenter) {
					target = ColorUtil.FindEdge(target + 1);
				}

				_targets[ld.Id] = target;
			}

			Enable = Data.Enable;
			_selectedGroup = Data.SelectedGroup;
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