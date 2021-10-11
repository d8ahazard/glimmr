#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models;
using Glimmr.Models.ColorSource;
using Glimmr.Models.ColorTarget;
using Glimmr.Models.Util;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Serilog;
using Color = System.Drawing.Color;

#endregion

namespace Glimmr.Services {
	// Handles capturing and sending color data
	public class ColorService : BackgroundService {
		public ControlService ControlService { get; }

		private DeviceMode _deviceMode;

		public readonly FrameCounter Counter;
		private readonly FrameSplitter _splitter;


		private readonly Dictionary<string, ColorSource> _streams;
		private readonly Stopwatch _watch;

		private bool _autoDisabled;
		private int _autoDisableDelay;
		private bool _demoComplete;

		private bool _enableAutoDisable;

		public Color[] LedColors { get; set; }

		public Color[] SectorColors { get; set; }

		public bool ColorsUpdated { get; set; }
		// Figure out how to make these generic, non-callable

		private IColorTarget[] _sDevices;
		private ColorSource? _stream;
		private bool _streamStarted;

		private Task? _streamTask;

		// Token for the color source
		private CancellationTokenSource _streamTokenSource;

		//private float _fps;
		private SystemData _systemData;

		// Token for the color target
		private CancellationTokenSource _targetTokenSource;

		public ColorService(ControlService controlService) {
			controlService.ColorService = this;
			_watch = new Stopwatch();
			_streamTokenSource = new CancellationTokenSource();
			_targetTokenSource = new CancellationTokenSource();
			_sDevices = Array.Empty<IColorTarget>();
			_systemData = DataUtil.GetSystemData();
			LedColors = new Color[_systemData.LedCount];
			SectorColors = new Color[+_systemData.SectorCount];
			_enableAutoDisable = _systemData.EnableAutoDisable;
			_streams = new Dictionary<string, ColorSource>();
			ControlService = controlService;
			Counter = new FrameCounter(this);
			ControlService.SetModeEvent += Mode;
			ControlService.DeviceReloadEvent += RefreshDeviceData;
			ControlService.RefreshLedEvent += ReloadLedData;
			ControlService.RefreshSystemEvent += ReloadSystemData;
			ControlService.TestLedEvent += LedTest;
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
					var args = new object[] {this};
					dynamic? obj = Activator.CreateInstance(Type.GetType(c)!, args);
					if (obj == null) {
						Log.Warning("Color source: " + tag + " is null.");
						continue;
					}

					var dObj = (ColorSource) obj;
					_streams[tag] = dObj;
				} catch (InvalidCastException e) {
					Log.Warning("Exception: " + e.Message + " at " + e.StackTrace);
				}
			}
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
			await Initialize();

			Log.Debug("Starting main Color Service loop...");
			await Task.Run(async () => {
				var cTask = ControlService.Execute(stoppingToken);
				var loopWatch = new Stopwatch();
				loopWatch.Start();
				while (!stoppingToken.IsCancellationRequested) {
					await CheckAutoDisable().ConfigureAwait(false);

					// Save a frame every 5 seconds
					if (loopWatch.Elapsed >= TimeSpan.FromSeconds(5)) {
						FrameSaveEvent.Invoke();
						loopWatch.Restart();
					}

					if (!ColorsUpdated) {
						continue;
					}

					if (!_demoComplete || _stream == null) {
						return;
					}

					Counter.Tick("");
					ColorsUpdated = false;
					await SendColors(LedColors, SectorColors);
				}

				if (cTask.IsCompleted) {
					Log.Debug("CTask canceled.");
				}
				loopWatch.Stop();
				Log.Information("Send loop canceled.");

				_streamTask?.Dispose();
				DataUtil.Dispose();
			}, CancellationToken.None);
		}


		private async Task Initialize() {
			Log.Information("Starting color service...");
			LoadData();
			Log.Debug("Data loaded...");
			if (!_systemData.SkipDemo) {
				Log.Debug("Executing demo...");
				await Demo(this, null).ConfigureAwait(true);
				_demoComplete = true;
				Log.Debug("Demo complete.");
			} else {
				_demoComplete = true;
				Log.Debug("Skipping demo.");
			}
			Log.Debug($"Previous AUTO state: {_systemData.AutoDisabled}, prevMode: {_systemData.PreviousMode}");
			var mode = _systemData.AutoDisabled ? _systemData.PreviousMode : _deviceMode;
			await Mode(this, new DynamicEventArgs(mode, true)).ConfigureAwait(true);
			Log.Information("Color service started.");
		}

		public override async Task StopAsync(CancellationToken cancellationToken) {
			Log.Information("Stopping color service...");
			await StopServices();
			DataUtil.Dispose();
			Log.Information("Color service stopped.");
			await base.StopAsync(cancellationToken);
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

			device.Testing = true;
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
			device.Testing = false;
			if (disable) {
				await device.StopStream();
			}

			ts.Cancel();
			ts.Dispose();
		}

		private async Task FlashSector(object o, DynamicEventArgs dynamicEventArgs) {
			var sector = dynamicEventArgs.Arg0;
			// When building center, we only need the v and h sectors.
			var dims = new[]
				{_systemData.VSectors, _systemData.VSectors, _systemData.HSectors, _systemData.HSectors};
			var builder = new FrameBuilder(dims, true, _systemData.UseCenter);
			var col = Color.FromArgb(255, 255, 0, 0);
			var emptyColors = ColorUtil.EmptyColors(_systemData.LedCount);
			var emptySectors = ColorUtil.EmptyColors(_systemData.SectorCount);
			emptySectors[sector - 1] = col;
			var tMat = builder.Build(emptySectors);
			foreach (var dev in _sDevices) {
				if (dev.Enable) {
					dev.Testing = true;
				}
			}

			_splitter.DoSend = false;
			await _splitter.Update(tMat);
			var colors = _splitter.GetColors().ToArray();
			var sectors = _splitter.GetSectors().ToArray();
			await SendColors(colors, sectors, 0, true);
			await Task.Delay(500);
			await SendColors(emptyColors, emptySectors, 0, true);
			await Task.Delay(500);
			await SendColors(colors, sectors, 0, true);
			await Task.Delay(1000);
			await SendColors(emptyColors, emptySectors, 0, true);
			foreach (var dev in _sDevices) {
				if (dev.Enable) {
					dev.Testing = false;
				}
			}

			_splitter.DoSend = true;
		}

		private async Task CheckAutoDisable() {
			// Don't do anything if auto-disable isn't enabled
			if (!_enableAutoDisable) {
				if (!_autoDisabled) {
					return;
				}

				_autoDisabled = false;
				DataUtil.SetItem("AutoDisabled", _autoDisabled);
				return;
			}

			var sourceActive = _stream?.SourceActive ?? false;
			//Log.Debug("Source is " + (sourceActive ? "Active" : "Inactive"));

			if (sourceActive) {
				// If our source is active, but not auto-disabled, do nothing
				if (_autoDisabled) {
					Log.Information("Auto-enabling stream.");
					_autoDisabled = false;
					DataUtil.SetItem("AutoDisabled", _autoDisabled);
					ControlService.SetModeEvent -= Mode;
					_deviceMode = _systemData.PreviousMode;
					await ControlService.SetMode(_deviceMode);
					await StartStream();
					ControlService.SetModeEvent += Mode;
				}

				_watch.Reset();
			} else {
				if (_autoDisabled) {
					return;
				}

				if (!_watch.IsRunning) {
					_watch.Restart();
				}

				if (_watch.ElapsedMilliseconds >= _autoDisableDelay * 1000f) {
					_autoDisabled = true;
					Counter.Reset();
					DataUtil.SetItem("AutoDisabled", _autoDisabled);
					ControlService.SetModeEvent -= Mode;
					await ControlService.SetMode(DeviceMode.Off, true);
					ControlService.SetModeEvent += Mode;
					await SendColors(ColorUtil.EmptyColors(_systemData.LedCount),
						ColorUtil.EmptyColors(_systemData.SectorCount), 0, true);
					Log.Information(
						$"Auto-disabling stream {_watch.ElapsedMilliseconds} vs {_autoDisableDelay * 1000}.");
					_watch.Reset();
					await StopStream();
				}
			}
		}


		private async Task LedTest(object o, DynamicEventArgs dynamicEventArgs) {
			if (dynamicEventArgs != null) {
				int led = dynamicEventArgs.Arg0;
				var colors = ColorUtil.EmptyList(_systemData.LedCount).ToArray();
				colors[led] = Color.FromArgb(255, 0, 0);
				var sectors = ColorUtil.LedsToSectors(colors.ToList(), _systemData).ToArray();
				var blackSectors = ColorUtil.EmptyList(_systemData.SectorCount).ToArray();
				await SendColors(colors, sectors, 0, true);
				await Task.Delay(500);
				await SendColors(colors, blackSectors, 0, true);
				await Task.Delay(500);
				await SendColors(colors, sectors, 0, true);
				await Task.Delay(500);
				await SendColors(colors, blackSectors, 0, true);
			}

			foreach (var dev in _sDevices) {
				dev.Testing = false;
			}
		}

		private void LoadData() {
			var sd = DataUtil.GetSystemData();
			// Reload main vars
			_deviceMode = sd.DeviceMode;
			_targetTokenSource = new CancellationTokenSource();
			_systemData = sd;
			LedColors = new Color[_systemData.LedCount];
			SectorColors = new Color[+_systemData.SectorCount];
			_enableAutoDisable = _systemData.EnableAutoDisable;
			_autoDisableDelay = _systemData.AutoDisableDelay;
			// Create new lists
			var sDevs = new List<IColorTarget>();
			var classes = SystemUtil.GetClasses<IColorTarget>();
			var deviceData = DataUtil.GetDevices();
			foreach (var c in classes) {
				try {
					var tag = c.Replace("Glimmr.Models.ColorTarget.", "");
					tag = tag.Split(".")[0];
					foreach (var device in deviceData.Where(device => device.Tag == tag)
						.Where(device => tag != "Led" || device.Id != "2")) {
						var args = new object[] {device, this};
						dynamic? obj = Activator.CreateInstance(Type.GetType(c)!, args);
						if (obj == null) {
							continue;
						}

						var dObj = (IColorTarget) obj;
						sDevs.Add(dObj);
					}
				} catch (InvalidCastException e) {
					Log.Warning("Exception: " + e.Message + " at " + e.StackTrace);
				}
			}

			_sDevices = sDevs.ToArray();
			Log.Information($"Loaded {_sDevices.Length} devices.");
		}

		private async Task Demo(object o, DynamicEventArgs? dynamicEventArgs) {
			await StartStream();
			var ledCount = 300;
			var sectorCount = _systemData.SectorCount;
			try {
				ledCount = _systemData.LedCount;
			} catch (Exception) {
				// ignored 
			}

			var i = 0;
			var cols = ColorUtil.EmptyColors(ledCount);
			var secs = ColorUtil.EmptyColors(sectorCount);
			try {
				while (i < ledCount) {
					var pi = i * 1.0f;
					var progress = pi / ledCount;
					var sector = (int) Math.Round(progress * sectorCount);
					var rCol = ColorUtil.Rainbow(progress);
					cols[i] = rCol;
					if (sector < secs.Length) {
						secs[sector] = rCol;
					}

					try {
						await SendColors(cols, secs, 0, true).ConfigureAwait(false);
					} catch (Exception e) {
						Log.Warning("SEND EXCEPTION: " + JsonConvert.SerializeObject(e));
					}

					i++;
				}
			} catch (Exception f) {
				Log.Warning("Outer demo exception: " + f.Message);
			}

			await Task.Delay(500);
		}

		private async Task RefreshDeviceData(object o, DynamicEventArgs dynamicEventArgs) {
			var id = dynamicEventArgs.Arg0;
			if (string.IsNullOrEmpty(id)) {
				Log.Warning("Can't refresh null device: " + id);
			}

			foreach (var dev in _sDevices) {
				if (dev.Data.Id != id) {
					continue;
				}

				Log.Debug("Reloading dev data...");
				await dev.ReloadData().ConfigureAwait(false);
				Log.Debug("Reloaded...");
				if (_deviceMode != DeviceMode.Off && dev.Data.Enable && !dev.Streaming || dev.Id == "0") {
					Log.Debug("Starting device stream.");
					await dev.StartStream(_targetTokenSource.Token);
					Log.Debug("Started...");
				}

				if (_deviceMode == DeviceMode.Off || dev.Data.Enable || !dev.Streaming) {
					Log.Debug("Mode is off or something, returning.");
					return;
				}

				Log.Information("Stopping disabled device: " + dev.Id);
				await dev.StopStream().ConfigureAwait(false);

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
					var args = new object[] {devData, this};
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
			var newMode = (DeviceMode) dynamicEventArgs.Arg0;
			bool init = dynamicEventArgs.Arg1 ?? false;
			if (init) {
				Log.Debug("Initializing mode.");
			}

			_deviceMode = newMode;
			// Don't unset auto-disable if init is set...
			if (_autoDisabled && !init) {
				_autoDisabled = false;
				DataUtil.SetItem("AutoDisabled", _autoDisabled);
				Log.Debug("Unsetting auto-disabled flag...");
			}

			_streamTokenSource.Cancel();

			if (_streamStarted && newMode == 0) {
				await StopStream();
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
				Log.Debug("Toggling stream for " + newMode);
				_streamTask = stream.ToggleStream(_streamTokenSource.Token);
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

		private Task StartStream() {
			var sc = 0;
			if (!_streamStarted) {
				_streamStarted = true;
				Log.Debug("Starting streaming targets...");
				foreach (var sDev in _sDevices) {
					try {
						if (!sDev.Enable && sDev.Id != "0") {
							continue;
						}

						sDev.StartStream(_targetTokenSource.Token);
						sc++;
					} catch (Exception e) {
						Log.Warning("Exception starting stream: " + e.Message);
					}
				}

				_streamStarted = true;
			}

			Log.Information($"Streaming started on {sc} devices.");
			return Task.CompletedTask;
		}

		private Task StopStream() {
			if (!_streamStarted) {
				return Task.CompletedTask;
			}

			Log.Information("Stopping device stream(s)...");
			_streamStarted = false;
			foreach (var dev in _sDevices) {
				if (dev.Enable || dev.Id == "0") {
					dev.StopStream();
				}
			}

			Log.Information("Stream(s) stopped on all devices.");
			return Task.CompletedTask;
		}

		private async Task SendColors(Color[] colors, Color[] sectors, int fadeTime = 0,
			bool force = false) {
			
			if (!_streamStarted) {
				return;
			}

			if (_autoDisabled && !force) {
				return;
			}
		
			if (ColorSendEventAsync != null) {
				try {
					await ColorSendEventAsync
						.InvokeAsync(this, new ColorSendEventArgs(colors, sectors, fadeTime, force)).ConfigureAwait(false);
					Counter.Tick("source");
				} catch (Exception e) {
					Log.Warning("Exception: " + e.Message + " at " + e.StackTrace);
				}

				
			}
		}


		private static void CancelSource(CancellationTokenSource target, bool dispose = false) {
			if (target == null) {
				return;
			}

			if (!target.IsCancellationRequested) {
				target.CancelAfter(0);
			}

			if (dispose) {
				target.Dispose();
			}
		}

		private async Task StopServices() {
			Log.Information("Stopping color services...");
			await StopStream().ConfigureAwait(false);
			_watch.Stop();
			//_frameWatch.Stop();
			_streamTokenSource.Cancel();
			CancelSource(_targetTokenSource, true);
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

		public async Task TriggerSend(Color[] leds, Color[] sectors, string source = "") {
			if (!_demoComplete || _stream == null) {
				return;
			}

			if (leds == null || sectors == null) {
				Log.Debug("No columns/sectors.");
			} else {
				Counter.Tick(source);
				await SendColors(leds, sectors).ConfigureAwait(false);
			}
		}
	}

	public class ColorSendEventArgs : EventArgs {
		public bool Force { get; }
		public Color[] LedColors { get; }
		public Color[] SectorColors { get; }
		public int FadeTime { get; }

		public ColorSendEventArgs(Color[] leds, Color[] sectors, int fadeTime, bool force) {
			LedColors = leds;
			SectorColors = sectors;
			FadeTime = fadeTime;
			Force = force;
		}
	}
}