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
using Newtonsoft.Json;
using Serilog;
using Color = System.Drawing.Color;

#endregion

namespace Glimmr.Models.ColorTarget.HueV2;

public sealed class HueV2Device : ColorTarget, IColorTarget, IDisposable {
	private HueV2Data Data { get; set; }
	private HueGroup? _group;
	private int _brightness;
	private StreamingHueClient? _client;
	private CancellationToken _ct;
	private bool _disposed;
	private EntertainmentLayer? _entLayer;
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

		Log.Debug($"{Data.Tag}::Starting stream: {Data.Id}...");
		ColorService.StartCounter++;
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
				if (added) break;
				foreach(var l in _group.Services) {
					if (added) break;
					if (m.Service == null) continue;
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
		ColorService.StartCounter--;
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
		ColorService.StopCounter++;
		
		_client.Close();
		Log.Debug($"{Data.Tag}::Stream stopped: {Data.Id}");
		ColorService.StopCounter--;
		await Task.FromResult(true);
	}

	private Task SetColors(object sender, ColorSendEventArgs args) {
		SetColor(args.SectorColors, args.FadeTime, args.Force);
		return Task.CompletedTask;
	}

	/// <summary>
	///     Update lights in entertainment group...
	/// </summary>
	/// <param name="sectors"></param>
	/// <param name="fadeTime"></param>
	/// <param name="force"></param>
	private void SetColor(IReadOnlyList<Color> sectors, int fadeTime, bool force = false) {
		if (!Streaming || !Enable || _entLayer == null || _group == null || Testing && !force) {
			return;
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
			if (target >= sectors.Count) {
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

		ColorService.Counter.Tick(Id);
	}


	private void SetData() {
		var sd = DataUtil.GetSystemData();
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
			var target = ld.TargetSector;

			if (sd.UseCenter) {
				Log.Debug("Setting center sectors?");
				target = ColorUtil.FindEdge(target + 1);
			}

			_targets[ld.Channel] = target;
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