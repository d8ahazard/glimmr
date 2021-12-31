#region

using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Led;

public class LedDevice : ColorTarget, IColorTarget {
	private readonly LedAgent? _agent;
	private LedData _data;

	public LedDevice(LedData ld, ColorService cs) : base(cs) {
		_data = ld;
		Id = _data.Id;

		_agent = cs.ControlService.GetAgent("LedAgent");

		// We only bind and create agent for LED device 0, regardless of which one is enabled.
		// The agent will handle all the color stuff for both strips.
		if (Id == "1") {
			return;
		}

		Log.Debug("LED device created.");
		cs.ColorSendEventAsync += SetColors;
	}

	public bool Streaming { get; set; }
	public bool Testing { get; set; }
	public string Id { get; }
	public bool Enable { get; set; }


	IColorTargetData IColorTarget.Data {
		get => _data;
		set => _data = (LedData)value;
	}

	public async Task StartStream(CancellationToken ct) {
		if (!SystemUtil.IsRaspberryPi()) {
			return;
		}

		if (Id != "0") {
			Log.Debug($"Id is {Id}, returning.");
			return;
		}

		_agent?.ReloadData();
		Log.Debug($"{_data.Tag}::Starting stream: {_data.Id}...");
		ColorService.StartCounter++;
		Streaming = true;
		await Task.FromResult(Streaming);
		Log.Debug($"{_data.Tag}::Stream started: {_data.Id}.");
		ColorService.StartCounter--;
	}

	public async Task StopStream() {
		if (!Streaming) {
			return;
		}

		Log.Debug($"{_data.Tag}::Stopping stream...{_data.Id}.");
		ColorService.StopCounter++;
		await StopLights();
		Streaming = false;
		Log.Debug($"{_data.Tag}::Stream stopped: {_data.Id}.");
		ColorService.StopCounter--;
	}


	public Task FlashColor(Color color) {
		_agent?.SetColor(color, Id);
		return Task.CompletedTask;
	}


	public void Dispose() {
		_agent?.Clear();
	}

	public Task ReloadData() {
		Log.Debug("Reload triggered for " + Id);
		if (Id == "1") {
			return Task.CompletedTask;
		}

		_agent?.ReloadData();
		return Task.CompletedTask;
	}

	private async Task SetColors(object sender, ColorSendEventArgs args) {
		SetColor(args.LedColors);
		await Task.FromResult(true);
	}


	private void SetColor(Color[] colors) {
		if (colors == null) {
			throw new ArgumentException("Invalid color input.");
		}

		if (Id == "0" && !Testing) {
			_agent?.SetColors(colors);
		}

		if (!Enable) {
			return;
		}

		ColorService.Counter.Tick(Id);
	}


	private async Task StopLights() {
		if (Id != "0") {
			return;
		}

		_agent?.Clear();
		await Task.FromResult(true);
	}
}