#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;
using YeelightAPI;

#endregion

namespace Glimmr.Models.ColorTarget.Yeelight; 

public class YeelightDevice : ColorTarget, IColorTarget {
	private string IpAddress { get; set; }
	private readonly ColorService _colorService;

	private readonly Device _yeeDevice;

	private float _brightness;

	private YeelightData _data;

	private Task? _streamTask;

	private int _targetSector;


	public YeelightDevice(YeelightData yd, ColorService cs) : base(cs) {
		_data = yd;
		Id = _data.Id;
		IpAddress = _data.IpAddress;
		Enable = _data.Enable;
		LoadData();
		Log.Debug("Created new yee device at " + yd.IpAddress);
		cs.ColorSendEventAsync += SetColors;
		_colorService = cs;

		_data.LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
		DataUtil.AddDeviceAsync(_data, false).ConfigureAwait(false);
		_yeeDevice = new Device(IpAddress);
		_colorService.ColorSendEventAsync += SetColors;
	}

	public bool Streaming { get; set; }
	public bool Testing { get; set; }
	public string Id { get; private set; }
	public bool Enable { get; set; }


	IColorTargetData IColorTarget.Data {
		get => _data;
		set => _data = (YeelightData)value;
	}


	public async Task StartStream(CancellationToken ct) {
		if (!Enable) {
			return;
		}

		Log.Debug($"{_data.Tag}::Starting stream: {_data.Id}...");
		ColorService.StartCounter++;
		_targetSector = _data.TargetSector;

		await _yeeDevice.Connect();
		var ip = IpUtil.GetLocalIpAddress();
		if (!string.IsNullOrEmpty(ip)) {
			_streamTask = _yeeDevice.StartMusicMode(ip);
			Streaming = true;
		}

		if (Streaming) {
			Log.Debug($"{_data.Tag}::Stream started: {_data.Id}.");
		}

		ColorService.StartCounter--;
	}

	public async Task StopStream() {
		if (!Streaming) {
			return;
		}

		Log.Debug($"{_data.Tag}::Stopping stream...{_data.Id}.");
		ColorService.StopCounter++;
		await FlashColor(Color.FromArgb(0, 0, 0));
		await _yeeDevice.StopMusicMode();
		_yeeDevice.Disconnect();
		Streaming = false;
		if (_streamTask is { IsCompleted: false }) {
			_streamTask.Dispose();
		}

		Log.Debug($"{_data.Tag}::Stream stopped: {_data.Id}.");
		ColorService.StopCounter--;
	}


	public async Task FlashColor(Color col) {
		await _yeeDevice.SetRGBColor(col.R, col.G, col.B).ConfigureAwait(false);
	}


	public Task ReloadData() {
		var dd = DataUtil.GetDevice<YeelightData>(Id);
		_data = dd ?? _data;
		return Task.CompletedTask;
	}


	public void Dispose() {
		_yeeDevice.Dispose();
	}

	private Task SetColors(object sender, ColorSendEventArgs args) {
		return SetColor(args.SectorColors, args.Force);
	}

	private async Task SetColor(IReadOnlyList<Color> sectors, bool force = false) {
		if (!Streaming || !Enable) {
			return;
		}

		if (!force) {
			if (!Streaming || _targetSector == -1 || Testing) {
				return;
			}
		}

		var col = sectors[_targetSector];
		if (_data.Brightness < 255) {
			col = ColorUtil.ClampBrightness(col, (int)_brightness);
		}

		if (_targetSector >= sectors.Count) {
			return;
		}

		await _yeeDevice.SetRGBColor(col.R, col.G, col.B);
		_colorService.Counter.Tick(Id);
	}

	private void LoadData() {
		var sd = DataUtil.GetSystemData();
		var prevIp = IpAddress;
		var restart = false;
		IpAddress = _data.IpAddress;
		if (!string.IsNullOrEmpty(prevIp) && prevIp != IpAddress) {
			Log.Debug("Restarting yee device...");
			if (Streaming) {
				restart = true;
				StopStream().ConfigureAwait(true);
				_yeeDevice.Dispose();
			}
		}

		var target = _data.TargetSector;

		if (sd.UseCenter) {
			target = ColorUtil.FindEdge(target + 1);
		}

		_brightness = _data.Brightness * 2.55f;
		_targetSector = target;
		Id = _data.Id;
		Enable = _data.Enable;

		if (restart) {
			StartStream(CancellationToken.None).ConfigureAwait(true);
		}

		Log.Debug("YEE: Data reloaded: " + Enable);
	}
}