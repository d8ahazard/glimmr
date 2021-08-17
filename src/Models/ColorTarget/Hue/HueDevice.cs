#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Q42.HueApi.ColorConverters;
using Q42.HueApi.Models.Groups;
using Q42.HueApi.Streaming;
using Q42.HueApi.Streaming.Extensions;
using Q42.HueApi.Streaming.Models;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Hue {
	public sealed class HueDevice : ColorTarget, IColorTarget, IDisposable {
		private HueData Data { get; set; }
		private int _brightness;
		private StreamingHueClient? _client;

		private CancellationToken _ct;
		private bool _disposed;

		private EntertainmentLayer? _entLayer;
		private string _ipAddress;
		private List<LightMap> _lightMappings;
		private string? _selectedGroup;
		private Dictionary<string, int> _targets;
		private string? _token;
		private Task? _updateTask;
		private string? _user;


		public HueDevice(HueData data, ColorService colorService) : base(colorService) {
			Data = data;
			_targets = new Dictionary<string, int>();
			_ipAddress = Data.IpAddress;
			_user = Data.User;
			_token = Data.Token;
			Enable = Data.Enable;
			_brightness = Data.Brightness;
			_lightMappings = Data.MappedLights;
			colorService.ColorSendEvent += SetColor;
			colorService.ControlService.RefreshSystemEvent += SetData;
			Id = Data.Id;
			_disposed = false;
			Streaming = false;
			_entLayer = null;
			_selectedGroup = Data.SelectedGroup;
			SetData();
		}


		public HueDevice(HueData data) {
			DataUtil.GetItem<int>("captureMode");
			Data = data;
			_lightMappings = Data.MappedLights;
			Id = Data.Id;
			_ipAddress = Data.IpAddress;
			_targets = new Dictionary<string, int>();
			_disposed = false;
			Streaming = false;
			SetData();
		}

		public bool Enable { get; set; }

		IColorTargetData IColorTarget.Data {
			get => Data;
			set => Data = (HueData) value;
		}

		public bool Testing { get; set; }
		public string Id { get; }
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

			_ct = ct;

			if (string.IsNullOrEmpty(_user) || string.IsNullOrEmpty(_token)) {
				Log.Information("No user or token, returning.");
				return;
			}

			Log.Information($"{Data.Tag}::Starting stream: {Data.Id}...");

			//Initialize streaming client
			_client = new StreamingHueClient(_ipAddress, _user, _token);

			//Get the entertainment group
			var all = await _client.LocalHueClient.GetEntertainmentGroups();
			Group? group = null;
			foreach (var g in all) {
				if (g.Id == _selectedGroup) {
					@group = g;
				}
			}

			if (group == null) {
				Log.Information("Unable to find selected streaming group.");
				return;
			}

			var lights = group.Lights;
			var mappedLights =
				(from ml in lights.SelectMany(light => _lightMappings.Where(ml => ml.Id.ToString() == light))
					where ml.TargetSector != -1
					select ml.Id).ToList();

			var entGroup = new StreamingGroup(mappedLights);

			//Create a streaming group
			_entLayer = entGroup.GetNewLayer();
			var connected = false;
			try {
				//Connect to the streaming group
				await _client.Connect(group.Id);
				connected = true;
			} catch (Exception) {
				Log.Information("Exception connecting to hue, re-trying.");
			}

			try {
				if (!connected) {
					await _client.Connect(@group.Id);
				}
			} catch (Exception e) {
				Log.Warning("Exception caught: " + e.Message);
				Streaming = false;
				return;
			}

			//Start auto updating this entertainment group
			_updateTask = _client.AutoUpdate(entGroup, ct, 60);

			Log.Information($"{Data.Tag}::Stream started: {Data.Id}");
			Streaming = true;
		}


		public Task ReloadData() {
			Log.Debug("Reloading Hue data...");
			var dev = DataUtil.GetDevice<HueData>(Id);
			if (dev == null) {
				return Task.CompletedTask;
			}

			Data = dev;
			SetData();
			return Task.CompletedTask;
		}

		/// <summary>
		///     Update lites in entertainment group...
		/// </summary>
		/// <param name="o"></param>
		/// <param name="args"></param>
		public Task SetColor(object o, DynamicEventArgs args) {
			SetColor(args.Arg0, args.Arg1, args.Arg2, args.Arg3);

			return Task.CompletedTask;
		}
		
		/// <summary>
		///     Update lites in entertainment group...
		/// </summary>
		/// <param name="list"></param>
		/// <param name="sectors"></param>
		/// <param name="fadeTime"></param>
		/// <param name="force"></param>
		public void SetColor(Color[] list, Color[] sectors, int fadeTime, bool force = false) {
			if (!Streaming || !Enable || _entLayer == null || Testing && !force) {
				return;
			}

			foreach (var entLight in _entLayer) {
				// Get data for our light from map

				var lightData = _lightMappings.SingleOrDefault(item =>
					item.Id == entLight.Id.ToString());
				// Return if not mapped
				if (lightData == null) {
					Log.Debug("No DATA");
					continue;
				}

				// Otherwise, get the corresponding sector color
				var target = _targets[entLight.Id.ToString()];
				if (target >= sectors.Length) {
					Log.Debug("NO TARGET!!");
					continue;
				}

				var color = sectors[target];
				var mb = lightData.Override ? lightData.Brightness : _brightness;
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

			ColorService?.Counter.Tick(Id);
		}


		public void Dispose() {
			Dispose(true);
		}


		public Task StopStream() {
			if (!Enable || !Streaming) {
				return Task.CompletedTask;
			}

			if (_client == null || _selectedGroup == null) {
				Log.Warning("Client or group are null, can't stop stream.");
				return Task.CompletedTask;
			}

			_client.LocalHueClient.SetStreamingAsync(_selectedGroup, false);
			_client.Close();
			Log.Information($"{Data.Tag}::Stream stopped: {Data.Id}");

			return Task.CompletedTask;
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
				if (lightId != entLight.Id) {
					continue;
				}

				// Get data for our light from map
				var oColor = new RGBColor(color.R, color.G, color.B);
				// If we're currently using a scene, animate it
				entLight.SetState(_ct, oColor, 255);
			}
		}


		private void SetData() {
			var sd = DataUtil.GetSystemData();
			_targets = new Dictionary<string, int>();
			_ipAddress = Data.IpAddress;
			_user = Data.User;
			_token = Data.Token;
			Enable = Data.Enable;
			_brightness = Data.Brightness;
			_lightMappings = Data.MappedLights;
			foreach (var ld in _lightMappings) {
				var target = ld.TargetSector;

				if (sd.UseCenter) {
					Log.Debug("Setting center sectors?");
					target = ColorUtil.FindEdge(target + 1);
				}

				_targets[ld.Id] = target;
			}

			Enable = Data.Enable;
			_selectedGroup = Data.SelectedGroup;
		}

		private void Dispose(bool disposing) {
			if (_disposed) {
				return;
			}

			_disposed = true;
			if (!disposing || _updateTask == null) {
				return;
			}

			if (!_updateTask.IsCompleted) {
				_updateTask.Dispose();
			}
		}
	}
}