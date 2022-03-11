#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using HueApi.ColorConverters;
using HueApi.Entertainment;
using HueApi.Entertainment.Extensions;
using HueApi.Entertainment.Models;
using HueApi.Models;
using Serilog;
using Color = System.Drawing.Color;

#endregion

namespace Glimmr.Models.ColorTarget.HueV2;

public sealed class HueV2Device : ColorTarget, IColorTarget, IDisposable {
	public bool Testing { get; set; }
	private HueV2Data Data { get; set; }
	private int _brightness;
	private StreamingHueClient? _client;
	private CancellationToken _ct;
	private bool _disposed;
	private EntertainmentLayer? _entLayer;
	private HueGroup? _group;
	private string _ipAddress;
	private string? _selectedGroup;
	private Dictionary<int, int> _targets;
	private string? _token;
	private Task? _updateTask;
	private string? _user;


	public HueV2Device(HueV2Data data, ColorService cs) : base(cs) {
		Data = data;
		_targets = new Dictionary<int, int>();
		_ipAddress = Data.IpAddress;
		_user = Data.AppKey;
		_token = Data.Token;
		Enable = Data.Enable;
		_brightness = Data.Brightness;
		cs.ControlService.RefreshSystemEvent += SetData;
		Id = Data.Id;
		_disposed = false;
		Streaming = false;
		_entLayer = null;
		_selectedGroup = Data.SelectedGroup;
		SetData();
		cs.ColorSendEventAsync += SetColors;
	}


	public bool Enable { get; set; }

	IColorTargetData IColorTarget.Data {
		get => Data;
		set => Data = (HueV2Data)value;
	}

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

		Log.Debug($"{Data.Tag}::Starting stream: {Data.Id}...");
		// Initialize streaming client
		_client = new StreamingHueClient(_ipAddress, _user, _token);

		// Ensure our streaming group still exists
		var all = await _client.LocalHueApi.GetEntertainmentConfigurations();

		EntertainmentConfiguration? group = null;
		foreach (var g in all.Data.Where(g => g.Id.ToString() == _selectedGroup)) {
			group = g;
		}

		_group = null;

		if (group == null) {
			Log.Information("Unable to find selected streaming group.");
			return;
		}

		foreach (var g in Data.Groups.Where(g => g.Id == _selectedGroup)) {
			_group = g;
		}

		if (_group == null) {
			Log.Information("Unable to find selected streaming group.");
			return;
		}

		var channels = group.Channels;
		var maps = new List<EntertainmentChannel>();
		foreach (var c in channels) {
			var added = false;
			foreach (var m in c.Members) {
				if (added) {
					break;
				}

				foreach (var l in _group.Services) {
					if (added) {
						break;
					}

					if (m.Service == null) {
						continue;
					}

					if (l.SvcId != m.Service.Rid) {
						continue;
					}

					maps.Add(c);
					added = true;
					break;
				}
			}
		}

		var entGroup = new StreamingGroup(maps);

		//Create a streaming group
		_entLayer = entGroup.GetNewLayer(true);
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
				await _client.Connect(group.Id);
			}
		} catch (Exception e) {
			Log.Warning("Exception caught: " + e.Message);
			Streaming = false;
			return;
		}

		//Start auto updating this entertainment group
		_updateTask = _client.AutoUpdate(entGroup, ct, 60);
		Log.Debug($"{Data.Tag}::Stream started: {Data.Id}");
		Streaming = true;
	}


	public Task ReloadData() {
		Log.Debug("Reloading Hue data...");
		var dev = DataUtil.GetDevice<HueV2Data>(Id);
		if (dev == null) {
			return Task.CompletedTask;
		}

		Data = dev;
		SetData();
		return Task.CompletedTask;
	}


	public void Dispose() {
		Dispose(true);
	}


	public async Task StopStream() {
		if (!Streaming) {
			return;
		}

		if (_client == null || _selectedGroup == null) {
			Log.Warning("Client or group are null, can't stop stream.");
			return;
		}

		Log.Debug($"{Data.Tag}::Stopping stream...{Data.Id}.");

		_client.Close();
		Log.Debug($"{Data.Tag}::Stream stopped: {Data.Id}");
		await Task.FromResult(true);
	}

	/// <summary>
	///     Update lights in entertainment group...
	/// </summary>
	/// <param name="_">LED colors, ignored for hue.</param>
	/// <param name="sectorColors"></param>
	public Task SetColors(IReadOnlyList<Color> _, IReadOnlyList<Color> sectorColors) {
		if (!Streaming || !Enable || _entLayer == null || _group == null) {
			return Task.CompletedTask;
		}


		foreach (var entLight in _entLayer) {
			//Get data for our light from map
			var lightData = _group.Services.SingleOrDefault(item =>
				item.Channel == entLight.Id);
			//Return if not mapped
			if (lightData == null) {
				Log.Debug("No DATA");
				continue;
			}

			// Otherwise, get the corresponding sector color
			var target = _targets[entLight.Id];
			if (target > sectorColors.Count || target == -1) {
				Log.Debug("NO TARGET!!");
				continue;
			}

			var color = sectorColors[target - 1];
			var mb = lightData.Override ? lightData.Brightness : _brightness;
			color = ColorUtil.ClampBrightness(color, mb);
			var oColor = new RGBColor(color.R, color.G, color.B);
			entLight.SetState(_ct, oColor, mb);
		}

		return Task.CompletedTask;
	}

	private async Task SetColors(object sender, ColorSendEventArgs args) {
		await SetColors(args.LedColors, args.SectorColors);
	}


	private void SetData() {
		_targets = new Dictionary<int, int>();
		_ipAddress = Data.IpAddress;
		_user = Data.AppKey;
		_token = Data.Token;
		Enable = Data.Enable;
		_brightness = Data.Brightness;
		_selectedGroup = Data.SelectedGroup;
		foreach (var g in Data.Groups.Where(g => g.Id == _selectedGroup)) {
			_group = g;
			break;
		}

		if (_group == null) {
			Log.Warning("No group selected for entertainment.");
			Enable = false;
			return;
		}

		foreach (var ld in _group.Services) {
			_targets[ld.Channel] = ld.TargetSector;
		}

		Enable = Data.Enable;
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