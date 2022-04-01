#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Glimmr.Enums;
using Glimmr.Models;
using Glimmr.Models.ColorSource;
using Glimmr.Models.ColorTarget;
using Glimmr.Models.Data;
using Glimmr.Models.Frame;
using Glimmr.Models.Helper;
using Glimmr.Models.Util;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Serilog;
using Timer = System.Timers.Timer;

#endregion

namespace Glimmr.Services;

// Handles capturing and sending color data
public class ColorService : BackgroundService {
	public bool Recording { get; set; }
	public ControlService ControlService { get; }

	public readonly FrameCounter Counter;
	private readonly Stopwatch _loopWatch;
	private readonly FrameSplitter _splitter;

	private readonly Dictionary<string, ColorSource> _streams;
	private readonly Stopwatch _watch;
	private int _adCount;
	private bool _autoDisabled;
	private int _autoDisableDelay;

	private DeviceMode _deviceMode;

	private bool _enableAutoDisable;

	private Dictionary<long, Color[]> _recording;

	private readonly Stopwatch _recordWatch;
	// Figure out how to make these generic, non-callable

	private IColorTarget[] _sDevices;
	private ColorSource? _stream;
	private bool _streamStarted;

	// Token for the color source
	private CancellationTokenSource _streamTokenSource;

	//private float _fps;
	private SystemData _systemData;

	// Token for the color target
	private CancellationTokenSource _targetTokenSource;
	private bool _testing;

	public ColorService() {
		ControlService.GetInstance().ColorService = this;
		_watch = new Stopwatch();
		_recordWatch = new Stopwatch();
		_recording = new Dictionary<long, Color[]>();
		_loopWatch = new Stopwatch();
		_streamTokenSource = new CancellationTokenSource();
		_targetTokenSource = new CancellationTokenSource();
		_sDevices = Array.Empty<IColorTarget>();
		_systemData = DataUtil.GetSystemData();
		_enableAutoDisable = _systemData.EnableAutoDisable;
		_streams = new Dictionary<string, ColorSource>();
		ControlService = ControlService.GetInstance();
		Counter = new FrameCounter(this);
		ControlService.SetModeEvent += Mode;
		ControlService.DeviceReloadEvent += RefreshDeviceData;
		ControlService.RefreshLedEvent += ReloadLedData;
		ControlService.RefreshSystemEvent += ReloadSystemData;
		ControlService.FlashLedEvent += FlashLed;
		ControlService.FlashDeviceEvent += FlashDevice;
		ControlService.FlashSectorEvent += FlashSector;
		ControlService.DemoLedEvent += Demo;
		_splitter = new FrameSplitter(this);
		LoadServices();
	}

	public event AsyncEventHandler<ColorSendEventArgs>? ColorSendEventAsync;

	public event Action FrameSaveEvent = delegate { };

	private void LoadServices() {
		var classes = SystemUtil.GetClasses<ColorSource>();
		foreach (var c in classes) {
			try {
				var tag = c.Replace("Glimmr.Models.ColorSource.", "");
				tag = tag.Split(".")[0];
				var args = new object[] { this };
				dynamic? obj = Activator.CreateInstance(Type.GetType(c)!, args);
				if (obj == null) {
					Log.Warning("Color source: " + tag + " is null.");
					continue;
				}

				var dObj = (ColorSource)obj;
				_streams[tag] = dObj;
			} catch (InvalidCastException e) {
				Log.Warning("Exception: " + e.Message + " at " + e.StackTrace);
			}
		}
	}

	protected override Task ExecuteAsync(CancellationToken stoppingToken) {
		Log.Debug("Starting color service...");
		var colorTask = Task.Run(async () => {
			await Initialize();
			_loopWatch.Start();

			var saveTimer = new Timer(1000);
			saveTimer.Elapsed += SaveFrame;
			saveTimer.Start();
			while (!stoppingToken.IsCancellationRequested) {
				await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);
			}

			saveTimer.Stop();
		}, CancellationToken.None);
		Log.Debug("Color Service Started.");
		return colorTask;
	}

	private void SaveFrame(object? sender, ElapsedEventArgs e) {
		FrameSaveEvent.Invoke();
	}

	public override Task StopAsync(CancellationToken cancellationToken) {
		Log.Information("Stopping color service...");
		_loopWatch.Stop();
		StopServices().ConfigureAwait(true);
		DataUtil.Dispose();
		Log.Information("Color service stopped.");
		_splitter.Dispose();
		base.StopAsync(cancellationToken);
		return Task.CompletedTask;
	}


	private async Task Initialize() {
		Log.Information("Initializing color service...");
		LoadData();
		Log.Debug("Data loaded...");
		if (!_systemData.SkipDemo) {
			Log.Debug("Executing demo...");
			await Demo(this, null).ConfigureAwait(true);
			Log.Debug("Demo complete.");
		} else {
			Log.Debug("Skipping demo.");
		}

		// If device was previously auto-disabled, set mode to last
		// mode before app restart, and see if there's a source.
		if (_systemData.AutoDisabled) {
			_systemData.AutoDisabled = false;
			_systemData.DeviceMode = _systemData.PreviousMode;
			DataUtil.SetSystemData(_systemData);
			_deviceMode = _systemData.DeviceMode;
		}

		await ControlService.SetMode(_deviceMode).ConfigureAwait(true);
		Log.Information("Color service started.");
	}


	public BackgroundService? GetStream(string name) {
		return _streams.ContainsKey(name) ? _streams[name] : null;
	}

	private async Task FlashDevice(object o, DynamicEventArgs dynamicEventArgs) {
		var devId = dynamicEventArgs.Arg0;
		var disable = false;
		var ts = new CancellationTokenSource();
		try {
		} catch (Exception) {
			// Ignored
		}

		var bColor = Color.FromArgb(0, 0, 0, 0);
		var rColor = Color.FromArgb(255, 255, 0, 0);
		IColorTarget? device = null;
		foreach (var t in _sDevices) {
			if (t.Id != devId) {
				continue;
			}

			device = t;
		}

		if (device == null) {
			Log.Warning("Unable to find target device.");
			return;
		}

		Log.Information("Flashing device: " + devId);
		if (!device.Streaming) {
			disable = true;
			await device.StartStream(ts.Token);
		}

		await device.FlashColor(rColor);
		Thread.Sleep(500);
		await device.FlashColor(bColor);
		Thread.Sleep(500);
		await device.FlashColor(rColor);
		Thread.Sleep(500);
		await device.FlashColor(bColor);
		if (disable) {
			await device.StopStream();
		}

		ts.Cancel();
		ts.Dispose();
	}

	private async Task FlashSector(object o, DynamicEventArgs dynamicEventArgs) {
		_testing = true;
		var sector = dynamicEventArgs.Arg0;
		// When building center, we only need the v and h sectors.
		var dims = new[]
			{ _systemData.VSectors, _systemData.VSectors, _systemData.HSectors, _systemData.HSectors };
		var builder = new FrameBuilder(dims, true, _systemData.UseCenter);
		var col = Color.FromArgb(255, 0, 0);
		var emptyColors = ColorUtil.EmptyColors(_systemData.LedCount);
		var emptySectors = ColorUtil.EmptyColors(_systemData.SectorCount);
		emptySectors[sector - 1] = col;
		var tMat = builder.Build(emptySectors);
		if (tMat == null) {
			Log.Debug("No mat, returning...");
			return;
		}

		var (colors, sectors) = _splitter.Update(tMat).Result;
		tMat.Dispose();
		await SendColors(colors, sectors, true);
		await Task.Delay(500);
		await SendColors(emptyColors, emptySectors, true);
		await Task.Delay(500);
		await SendColors(colors, sectors, true);
		await Task.Delay(1000);
		await SendColors(emptyColors, emptySectors, true);
		_testing = false;
		builder.Dispose();
		Log.Debug("Sector flash complete.");
	}

	public void CheckAutoDisable(bool sourceActive) {
		// Don't do anything if auto-disable isn't enabled
		if (!_enableAutoDisable) {
			if (!_autoDisabled) {
				return;
			}

			_autoDisabled = false;
			DataUtil.SetItem("AutoDisabled", _autoDisabled);
			return;
		}

		if (_deviceMode == DeviceMode.Off) {
			return;
		}

		if (sourceActive) {
			// If our source is active and we're auto-disabled, turn it off.
			_adCount = 0;
			if (!_autoDisabled) {
				return;
			}

			Log.Information("Auto-enabling stream.");
			_autoDisabled = false;
			DataUtil.SetItem("AutoDisabled", _autoDisabled);
			ControlService.SetModeEvent -= Mode;
			_deviceMode = _systemData.PreviousMode;
			ControlService.SetMode(_deviceMode).ConfigureAwait(false);
			StartStream().ConfigureAwait(false);
			ControlService.SetModeEvent += Mode;
		} else {
			if (_autoDisabled) {
				_adCount = _autoDisableDelay;
				return;
			}

			_adCount++;

			if (_adCount < _autoDisableDelay) {
				return;
			}

			_adCount = _autoDisableDelay;
			Log.Debug($"Auto-disabling: {_adCount}/{_autoDisableDelay}");
			_autoDisabled = true;
			Counter.Reset();
			DataUtil.SetItem("AutoDisabled", _autoDisabled);
			ControlService.SetModeEvent -= Mode;
			ControlService.SetMode(DeviceMode.Off, true).ConfigureAwait(false);
			ControlService.SetModeEvent += Mode;
			SendColors(ColorUtil.EmptyColors(_systemData.LedCount),
				ColorUtil.EmptyColors(_systemData.SectorCount), true).ConfigureAwait(false);
			Log.Information("Auto-disabling stream.");
			StopDevices().ConfigureAwait(false);
		}
	}


	private async Task FlashLed(object o, DynamicEventArgs dynamicEventArgs) {
		_testing = true;
		int led = dynamicEventArgs.Arg0;
		var colors = ColorUtil.EmptyList(_systemData.LedCount).ToArray();
		colors[led] = Color.FromArgb(255, 0, 0);
		var sectors = ColorUtil.LedsToSectors(colors.ToList(), _systemData).ToArray();
		var blackSectors = ColorUtil.EmptyList(_systemData.SectorCount).ToArray();
		await SendColors(colors, sectors, true);
		await Task.Delay(500);
		await SendColors(colors, blackSectors, true);
		await Task.Delay(500);
		await SendColors(colors, sectors, true);
		await Task.Delay(1000);
		await SendColors(colors, blackSectors, true);
		_testing = false;
	}

	private void LoadData() {
		var sd = DataUtil.GetSystemData();
		// Reload main vars
		_deviceMode = sd.DeviceMode;
		_targetTokenSource = new CancellationTokenSource();
		_systemData = sd;
		_enableAutoDisable = _systemData.EnableAutoDisable;
		_autoDisableDelay = _systemData.AutoDisableDelay;
		// Create new lists
		var sDevs = new List<IColorTarget>();
		var classes = SystemUtil.GetClasses<IColorTarget>();
		var deviceData = DataUtil.GetDevices();
		foreach (var c in classes) {
			try {
				sDevs.AddRange(GetDevice(deviceData, c));
			} catch (InvalidCastException e) {
				Log.Warning("Exception: " + e.Message + " at " + e.StackTrace);
			}
		}

		_sDevices = sDevs.ToArray();
		Log.Information($"Loaded {_sDevices.Length} devices.");
	}

	private IEnumerable<IColorTarget> GetDevice(IEnumerable<dynamic> devices, string c) {
		var output = new List<IColorTarget>();
		var tag = c.Replace("Glimmr.Models.ColorTarget.", "");
		tag = tag.Split(".")[0];

		foreach (IColorTargetData device in devices.Where(device => device.Tag == tag)) {
			switch (tag) {
				case "Hue":
					continue;
				default:
					try {
						var cType = Type.GetType(c);
						if (cType == null) {
							Log.Warning("Unable to load type for " + c);
							continue;
						}

						if (Activator.CreateInstance(cType, device, this) is not IColorTarget dev) {
							Log.Warning("Unable to load instance for " + c);
							continue;
						}

						output.Add(dev);
					} catch (Exception e) {
						Log.Warning($"Exception adding {tag} ({c}) device {device.Id}: " + e.Message + " at " +
						            e.StackTrace);
					}

					break;
			}
		}

		return output;
	}

	private async Task Demo(object o, DynamicEventArgs? dynamicEventArgs) {
		await StartStream();
		var dims = new[] { 20, 20, 40, 40 };
		var builder = new FrameBuilder(dims);
		var ledCount = dims.Sum();
		var i = 0;
		var cols = ColorUtil.EmptyColors(ledCount);
		try {
			_splitter.DoSend = false;
			while (i < ledCount) {
				var pi = i * 1.0f;
				var progress = pi / ledCount;
				var rCol = ColorUtil.Rainbow(progress);
				cols[i] = rCol;
				var frame = builder.Build(cols);
				if (frame != null) {
					var (colors, sectors) = _splitter.Update(frame).Result;
					frame.Dispose();
					try {
						await SendColors(colors, sectors, true).ConfigureAwait(false);
					} catch (Exception e) {
						Log.Warning("SEND EXCEPTION: " + JsonConvert.SerializeObject(e));
					}
				}

				i++;
			}

			_splitter.DoSend = true;
		} catch (Exception f) {
			Log.Warning("Outer demo exception: " + f.Message);
		}

		builder.Dispose();
		await Task.Delay(500);
	}

	private async Task RefreshDeviceData(object o, DynamicEventArgs dynamicEventArgs) {
		var id = dynamicEventArgs.Arg0;
		if (string.IsNullOrEmpty(id)) {
			Log.Warning("Can't refresh null device: " + id);
		}

		// REALLY reload device 0 if we update LED 1
		if (id == "1") {
			id = "0";
		}

		foreach (var dev in _sDevices) {
			if (dev.Data.Id != id) {
				continue;
			}

			Log.Debug($"Reloading data for {dev.Data.Name} ({dev.Data.Id})");
			var enabled = dev.Enable;

			await dev.ReloadData();
			var doEnable = dev.Enable;
			if (_deviceMode != DeviceMode.Off && doEnable && !enabled && !dev.Streaming) {
				await dev.StartStream(_targetTokenSource.Token);
				return;
			}

			if (id == "0") {
				if (!doEnable) {
					await dev.FlashColor(Color.Black);
				}
				return;
			}

			if (enabled && !doEnable && dev.Streaming) {
				Log.Information("Stopping disabled device: " + dev.Id);
				await dev.StopStream().ConfigureAwait(false);
			}
			return;
		}

		var sda = DataUtil.GetDevice(id);

		// If our device is a real boy, start it and add it
		if (sda == null) {
			return;
		}

		var newDev = CreateDevice(sda);
		await newDev.StartStream(_targetTokenSource.Token).ConfigureAwait(false);

		var sDevs = _sDevices.ToList();
		sDevs.Add(newDev);
		_sDevices = sDevs.ToArray();
	}

	private dynamic? CreateDevice(dynamic devData) {
		var classes = SystemUtil.GetClasses<IColorTarget>();
		foreach (var c in classes) {
			try {
				var tag = c.Replace("Glimmr.Models.ColorTarget.", "");
				tag = tag.Split(".")[0];
				if (devData.Tag != tag) {
					continue;
				}

				Log.Debug($"Creating {devData.Tag}: {devData.Id}");
				var args = new object[] { devData, this };
				dynamic? obj = Activator.CreateInstance(Type.GetType(c)!, args);
				return obj;
			} catch (Exception e) {
				Log.Warning("Exception creating device: " + e.Message + " at " + e.StackTrace);
			}
		}

		return null;
	}

	private Task ReloadLedData(object o, DynamicEventArgs dynamicEventArgs) {
		string ledId = dynamicEventArgs.Arg0;
		foreach (var dev in _sDevices) {
			if (dev.Id == ledId) {
				dev.ReloadData();
			}
		}

		return Task.CompletedTask;
	}

	private void ReloadSystemData() {
		var sd = _systemData;
		_systemData = DataUtil.GetSystemData();

		_autoDisableDelay = sd.AutoDisableDelay;
		_enableAutoDisable = _systemData.EnableAutoDisable;

		if (_autoDisableDelay < 1) {
			_autoDisableDelay = 10;
		}
	}

	private async Task Mode(object o, DynamicEventArgs dynamicEventArgs) {
		var sd = DataUtil.GetSystemData();
		var newMode = (DeviceMode)dynamicEventArgs.Arg0;
		Log.Debug("New mode set: " + newMode);
		bool init = dynamicEventArgs.Arg1 ?? false;
		if (init) {
			Log.Debug("Initializing mode.");
		}

		_deviceMode = newMode;
		// Don't unset auto-disable if init is set...
		if (sd.AutoDisabled && !init) {
			_autoDisabled = false;
			DataUtil.SetItem("AutoDisabled", _autoDisabled);
			Log.Debug("Unsetting auto-disabled flag...");
		}

		_streamTokenSource.Cancel();
		await Task.Delay(TimeSpan.FromMilliseconds(500));
		if (_stream is { SourceActive: true }) {
			Log.Debug("Killing stream!");
			_stream.Stop();
		}

		if (_streamStarted && newMode == 0) {
			await StopDevices();
		}


		_streamTokenSource = new CancellationTokenSource();
		if (_streamTokenSource.IsCancellationRequested) {
			Log.Warning("Token source has cancellation requested.");
			//return;
		}

		// Load our stream regardless
		ColorSource? stream = null;
		if (newMode == DeviceMode.Udp) {
			stream = sd.StreamMode == StreamMode.DreamScreen
				? _streams["DreamScreen"]
				: _streams["UDP"];
		} else if (newMode != DeviceMode.Off) {
			stream = _streams[newMode.ToString()];
		}

		_stream = stream;
		if (stream != null) {
			Log.Debug("Setting stream mode to " + newMode);
			await stream.Start(_streamTokenSource.Token);
			Log.Debug("Stream started.");
			_stream = stream;
		} else {
			if (newMode != DeviceMode.Off) {
				Log.Warning("Unable to acquire stream.");
			}
		}

		if (newMode != 0 && !_streamStarted && !_autoDisabled) {
			await StartStream();
		}

		_deviceMode = newMode;
		Log.Information($"Device mode updated to {newMode}.");
	}

	private async Task StartStream() {
		if (!_streamStarted) {
			_streamStarted = true;

			// Cancel any attempts to start streaming after four seconds if unsuccessful
			var cts = new CancellationTokenSource();
			var sTasks = new List<Task>();
			Log.Debug("Starting streaming targets...");
			cts.CancelAfter(TimeSpan.FromSeconds(4));
			var enableCount = 0;
			foreach (var sDev in _sDevices) {
				if (!sDev.Enable && sDev.Id != "0") {
					continue;
				}

				enableCount++;
				sTasks.Add(WrapTask(sDev.StartStream(_targetTokenSource.Token), cts.Token));
			}

			try {
				await Task.WhenAll(sTasks);
			} catch (Exception e) {
				Log.Warning("Exception starting dev: " + e.Message);
			}

			var startCount = _sDevices.Count(dev => dev.Enable && dev.Streaming);
			_streamStarted = true;
			Log.Information($"Streaming started on {startCount}/{enableCount} devices.");
		}
	}

	private Task WrapTask(Task toWrap, CancellationToken token) {
		return Task.Run(async () => {
			try {
				await Task.Run(async () => {
					try {
						await toWrap;
					} catch (Exception e) {
						Log.Debug("Exception with wrapped task: " + e.Message);
					}
				}, token);
			} catch (TaskCanceledException) {
				Log.Debug("Task canceled...");
			}
		}, CancellationToken.None);
	}

	private async Task StopDevices() {
		if (!_streamStarted) {
			return;
		}

		Log.Information("Stopping streaming...");
		_streamStarted = false;
		// Give our devices four seconds to stop streaming, then cancel so we're not waiting forever...
		var cts = new CancellationTokenSource();
		cts.CancelAfter(TimeSpan.FromSeconds(5));
		var en = 0;
		var toKill = new List<Task>();
		var killSource = new CancellationTokenSource();
		killSource.CancelAfter(TimeSpan.FromSeconds(4));
		foreach (var dev in _sDevices) {
			try {
				if (dev.Enable) {
					en++;
				}

				if (!dev.Streaming) {
					continue;
				}

				toKill.Add(WrapTask(dev.StopStream(), killSource.Token));
			} catch (Exception) {
				// Ignored.
			}
		}

		await Task.WhenAll(toKill);
		var len = _sDevices.Count(dev => dev.Enable && !dev.Streaming);
		Log.Information($"Streaming stopped on {len}/{en} devices.");
	}

	public async Task SendColors(Color[] colors, Color[] sectors, bool force = false) {
		if (!_streamStarted) {
			return;
		}

		if (_autoDisabled && !force) {
			return;
		}

		if (_testing && !force) {
			return;
		}

		if (ColorSendEventAsync != null) {
			try {
				await ColorSendEventAsync
					.InvokeAsync(this, new ColorSendEventArgs(colors, sectors))
					.ConfigureAwait(false);
				if (Recording) {
					var time = _recordWatch.ElapsedMilliseconds;
					_recording[time] = sectors;
				}

				Counter.Tick("source");
			} catch (Exception e) {
				Log.Warning("Exception: " + e.Message + " at " + e.StackTrace);
			}
		}
	}


	private static void CancelSource(CancellationTokenSource target, bool dispose = false) {
		if (!target.IsCancellationRequested) {
			target.CancelAfter(0);
		}

		if (dispose) {
			target.Dispose();
		}
	}

	private async Task StopServices() {
		Log.Information("Stopping color services...");
		await StopDevices().ConfigureAwait(false);
		_watch.Stop();
		//_frameWatch.Stop();
		_streamTokenSource.Cancel();
		CancelSource(_targetTokenSource, true);
		foreach (var (_, svc) in _streams) {
			svc.Splitter.Dispose();
			svc.Builder?.Dispose();
		}

		foreach (var s in _sDevices) {
			try {
				s.Dispose();
			} catch (Exception e) {
				Log.Warning("Caught exception: " + e.Message);
			}
		}

		Log.Information("All services have been stopped.");
	}

	public void StopDevice(string id, bool remove = false) {
		var devs = new List<IColorTarget>();
		foreach (var dev in _sDevices) {
			if (dev.Id == id) {
				if (dev.Enable && dev.Streaming) {
					dev.StopStream();
				}

				dev.Enable = false;
				if (!remove) {
					devs.Add(dev);
				} else {
					dev.Dispose();
				}
			} else {
				devs.Add(dev);
			}
		}

		_sDevices = devs.ToArray();
	}

	public void StartRecording() {
		if (Recording) {
			Log.Debug("Recording already in progress.");
		}

		_recording = new Dictionary<long, Color[]>();
		var rts = new CancellationTokenSource();
		rts.CancelAfter(TimeSpan.FromSeconds(30));
		Task.Run(async () => {
			Log.Debug("Starting recording...");
			_recordWatch.Restart();
			Recording = true;
			while (!rts.IsCancellationRequested) {
				await Task.Delay(500, CancellationToken.None);
			}

			_recordWatch.Stop();
			Log.Debug("Recording completed.");
			Recording = false;
			var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
			try {
				var userDir = SystemUtil.GetUserDir();
				var path = Path.Join(userDir, "rec" + timestamp + ".rec");
				await File.WriteAllTextAsync(path,
					JsonConvert.SerializeObject(_recording), CancellationToken.None);
				Log.Debug("Recording saved to " + path);
			} catch (Exception e) {
				Log.Warning("Exception saving rec file: " + e.Message);
			}
		}, CancellationToken.None);
	}
}

public class ColorSendEventArgs : EventArgs {
	public Color[] LedColors { get; }
	public Color[] SectorColors { get; }

	public ColorSendEventArgs(Color[] leds, Color[] sectors) {
		LedColors = leds;
		SectorColors = sectors;
	}
}