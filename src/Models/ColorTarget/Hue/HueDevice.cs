#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
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

		private StreamingHueClient? _client;
		private StreamingGroup? _stream;
		private EntertainmentLayer? _entLayer;
		private string? _token;
		private string? _user;

		private CancellationToken _ct;
		private bool _disposed;
		private List<LightMap> _lightMappings;
		private string? _selectedGroup;
		private Group? _streamingGroup;
		private Dictionary<string, int> _targets;
		private Task _uTask;
		public bool Enable { get; set; }

		IColorTargetData IColorTarget.Data {
			get => Data;
			set => Data = (HueData) value;
		}

		public bool Testing { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; } = "Hue";
		public bool Streaming { get; set; }


		public HueDevice(HueData data, ColorService colorService) : base(colorService) {
			Data = data;
			_targets = new Dictionary<string, int>();
			IpAddress = Data.IpAddress;
			Tag = Data.Tag;
			_user = Data.User;
			_token = Data.Token;
			Enable = Data.Enable;
			Brightness = Data.Brightness;
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
			IpAddress = Data.IpAddress;
			_targets = new Dictionary<string, int>();
			_disposed = false;
			Streaming = false;
			SetData();
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

			if (_selectedGroup == null) {
				Log.Information("No group selected, returning.");
				Streaming = false;
				return;
			}

			
			if (_user != null && _token != null && Enable) {
				_client = new StreamingHueClient(IpAddress, _user, _token);
			} else {
				Log.Warning("Can't create client.");
				Streaming = false;
				return;
			}

			_streamingGroup = _client.LocalHueClient.GetGroupAsync(_selectedGroup).Result;
			if (_streamingGroup == null) {
				Log.Warning("Unable to fetch group with ID of " + _selectedGroup);
			} else {
				try {
					await _client.Connect(_streamingGroup.Id);
					_stream = SetupAndReturnGroup().Result;
				} catch (Exception e) {
					Log.Warning("Streaming exception caught: " + e.Message + " at " + e.StackTrace);
				}
			}

			if (_stream == null) {
				Log.Warning("Unable to create stream!");
				Streaming = false;
				return;
			}
			
			_entLayer = _stream.GetNewLayer(true);
			_uTask = _client.AutoUpdate(_stream, ct);
			//Connect to the streaming group
			
			Log.Information($"{Data.Tag}::Stream started: {Data.Id}");
			Streaming = true;
		}


		public Task ReloadData() {
			Log.Debug("Reloading Hue data...");
			var dev = DataUtil.GetDevice<HueData>(Id);
			if (dev == null) return Task.CompletedTask;
			Data = dev;
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
			
			ColorService?.Counter.Tick(Id);
		}


		public void Dispose() {
			Dispose(true);
		}


		public Task StopStream() {
			if (!Enable || !Streaming) {
				return Task.CompletedTask;
			}

			try {
				if (_client == null || _selectedGroup == null) {
					Log.Debug("Client or group is null, returning...stream stopped?");
					return Task.CompletedTask;
				}

				_client.LocalHueClient.SetStreamingAsync(_selectedGroup, false);
				if (!_uTask.IsCompleted) _uTask.Dispose();
				Streaming = false;
				Log.Information($"{Data.Tag}::Stream stopped: {Data.Id}");
			} catch (Exception) {
				// ignored
			}

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
				if (lightId == entLight.Id) {
					// Get data for our light from map
					var oColor = new RGBColor(color.R, color.G, color.B);
					// If we're currently using a scene, animate it
					entLight.SetState(_ct, oColor, 255);
				}
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
			_lightMappings = Data.MappedLights;
			foreach (var ld in _lightMappings) {
				var target = ld.TargetSector;
				
				if (sd.UseCenter) {
					target = ColorUtil.FindEdge(target + 1);
				}

				_targets[ld.Id] = target;
			}

			Enable = Data.Enable;
			var prevGroup = _selectedGroup;
			_selectedGroup = Data.SelectedGroup;
		}

		private async Task<StreamingGroup?> SetupAndReturnGroup() {
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
				_client?.Dispose();
			}

			_disposed = true;
		}
	}
}