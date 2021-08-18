﻿#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models;
using Glimmr.Models.AsyncEvent;
using Glimmr.Models.ColorSource;
using Glimmr.Models.ColorTarget;
using Glimmr.Models.Util;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Services {
	// Handles capturing and sending color data
	public class ColorService : BackgroundService {
		public ControlService ControlService { get; }
		public event AsyncEventHandler<ColorSendEventArgs>? ColorSendEventAsync;

		
		public event Action FrameSaveEvent = delegate { };

		public readonly FrameCounter Counter;
		public readonly FrameSplitter Splitter;


		private readonly Dictionary<string, IColorSource> _streams;
		private readonly Stopwatch _watch;
		
		public DeviceMode DeviceMode { get; private set; }
		
		private bool _autoDisabled;
		private int _autoDisableDelay;

		private bool _enableAutoDisable;
		// Figure out how to make these generic, non-callable

		private IColorTarget[] _sDevices;
		private bool _streamStarted;
		private IColorSource? _stream;
		private Task? _streamTask;
		private bool _sending;
		
		private bool _demoComplete;
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
			_enableAutoDisable = _systemData.EnableAutoDisable;
			_streams = new Dictionary<string, IColorSource>();
			ControlService = controlService;
			Counter = new FrameCounter(this);
			ControlService.ColorService = this;
			ControlService.SetModeEvent += Mode;
			ControlService.DeviceReloadEvent += RefreshDeviceData;
			ControlService.RefreshLedEvent += ReloadLedData;
			ControlService.RefreshSystemEvent += ReloadSystemData;
			ControlService.TestLedEvent += LedTest;
			ControlService.FlashDeviceEvent += FlashDevice;
			ControlService.FlashSectorEvent += FlashSector;
			ControlService.DemoLedEvent += Demo;
			Splitter = new FrameSplitter(this);
			LoadServices();
		}

		private void LoadServices() {
			var classes = SystemUtil.GetClasses<IColorSource>();
			foreach (var c in classes) {
				try {
					var tag = c.Replace("Glimmr.Models.ColorSource.", "");
					tag = tag.Split(".")[0];
					var args = new object[] {this};
					dynamic? obj = Activator.CreateInstance(Type.GetType(c)!, args);
					if (obj == null) {
						continue;
					}

					var dObj = (IColorSource) obj;
					Log.Debug("Adding color source: " + tag);
					_streams[tag] = dObj;
				} catch (InvalidCastException e) {
					Log.Warning("Exception: " + e.Message + " at " + e.StackTrace);
				}
			}
		}

		protected override Task ExecuteAsync(CancellationToken stoppingToken) {
			Initialize();
			
			Log.Debug("Send task started, starting main loop.");
			return Task.Run(async () => {
				var loopWatch = new Stopwatch();
				loopWatch.Start();
				var fc = 0;
				// 30FPS
				const int ms = 30;
				while (!stoppingToken.IsCancellationRequested) {
					loopWatch.Restart();
					await CheckAutoDisable();
					if (!_demoComplete || _stream == null) {
						if (_demoComplete && _stream == null) {
							Log.Warning("Stream is null.");
						}
						continue;
					}
				
					var cols = _stream.GetColors();
					var secs = _stream.GetSectors();
					if (cols == null || secs == null) {
						Log.Debug("No columns/sectors.");
					} else {
						await SendColors(cols, secs);
						fc++;
					}
					// Save a frame every 5 seconds
					if (fc >= 150) {
						fc = 0;
						FrameSaveEvent.Invoke();
					}
					var time = loopWatch.ElapsedMilliseconds;
					if (time < ms) {
						var diff = ms - time;
						await Task.Delay(TimeSpan.FromMilliseconds(diff), CancellationToken.None);
					}
				}
				loopWatch.Stop();
				Log.Information("Send loop canceled.");
				
				_streamTask?.Dispose();
				DataUtil.Dispose();
			}, CancellationToken.None);
		}


		private void Initialize() {
			Log.Information("Starting color service...");
			LoadData();
			Log.Debug("Data loaded...");
			if (!_systemData.SkipDemo) {
				Log.Information("Executing demo...");
				Demo(this, null).ConfigureAwait(true);
				_demoComplete = true;
				Log.Information("Demo complete.");
			} else {
				_demoComplete = true;
				Log.Information("Skipping demo.");
			}

			Mode(this, new DynamicEventArgs(DeviceMode, true)).ConfigureAwait(true);
			Log.Information($"Color service started, device mode is {DeviceMode}.");
		}

		public override async Task StopAsync(CancellationToken cancellationToken) {
			Log.Information("Stopping color service...");
			await StopServices();
			DataUtil.Dispose();
			Log.Information("Color service stopped.");
			await base.StopAsync(cancellationToken);
		}

		public BackgroundService? GetStream(string name) {
			if (_streams.ContainsKey(name)) {
				return (BackgroundService) _streams[name];
			}
			return null;
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
				{_systemData.VSectors, 0, _systemData.HSectors, 0};
			var builder = new FrameBuilder(dims,true, true);
			var col = Color.FromArgb(255, 255, 0, 0);
			var emptyColors = ColorUtil.EmptyColors(_systemData.SectorCount);
			var emptySectors = ColorUtil.EmptyColors(_systemData.LedCount);
			var bSectors = emptySectors;
			bSectors[sector - 1] = col;
			var tMat = builder.Build(bSectors);
			foreach (var dev in _sDevices) {
				if (dev.Enable) dev.Testing = true;
			}

			Splitter.DoSend = false;
			await Splitter.Update(tMat);
			var colors = Splitter.GetColors().ToArray();
			var sectors = Splitter.GetSectors().ToArray();
			await SendColors(colors, sectors, 0, true);
			await Task.Delay(500);
			await SendColors(emptyColors, emptySectors, 0, true);
			await Task.Delay(500);
			await SendColors(colors, sectors, 0, true);
			await Task.Delay(1000);
			await SendColors(emptyColors, emptySectors, 0, true);
			foreach (var dev in _sDevices) {
				if (dev.Enable) dev.Testing = true;
			}
			Splitter.DoSend = true;
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

			if (sourceActive) {
				// If our source is active, but not auto-disabled, do nothing
				if (_autoDisabled) {
					Log.Information("Auto-enabling stream.");
					_autoDisabled = false;
					DataUtil.SetItem("AutoDisabled", _autoDisabled);
					ControlService.SetModeEvent -= Mode;
					await ControlService.SetMode((int) DeviceMode);
					if (!_streamStarted) await StartStream();
					ControlService.SetModeEvent += Mode;
				}

				_watch.Reset();
			} else {
				if (!_watch.IsRunning)_watch.Restart();

				if (_watch.ElapsedMilliseconds >= _autoDisableDelay * 1000f) {
					_autoDisabled = true;
					Counter.Reset();
					DataUtil.SetItem("AutoDisabled", _autoDisabled);
					ControlService.SetModeEvent -= Mode;
					await ControlService.SetMode(0);
					ControlService.SetModeEvent += Mode;
					Log.Information(
						$"Auto-disabling stream {_watch.ElapsedMilliseconds} vs {_autoDisableDelay * 1000}.");
					_watch.Reset();
					if (_streamStarted) await StopStream();
				}
			}
		}


		private async Task LedTest(object o, DynamicEventArgs dynamicEventArgs) {
			int led = dynamicEventArgs.Arg0;
			var colors = ColorUtil.EmptyList(_systemData.LedCount).ToArray();
			var blackColors = colors;
			colors[led] = Color.FromArgb(255, 0, 0);
			var sectors = ColorUtil.LedsToSectors(colors.ToList(), _systemData).ToArray();
			var blackSectors = ColorUtil.EmptyList(_systemData.SectorCount).ToArray();
			await SendColors(colors, sectors, 0, true);
			await Task.Delay(500);
			await SendColors(blackColors, blackSectors, 0, true);
			await Task.Delay(500);
			await SendColors(colors, sectors, 0, true);
			await Task.Delay(500);
			await SendColors(blackColors, blackSectors, 0, true);

			foreach (var dev in _sDevices) {
				dev.Testing = false;
			}
		}

		private void LoadData() {
			var sd = DataUtil.GetSystemData();
			// Reload main vars
			DeviceMode = (DeviceMode) sd.DeviceMode;
			_targetTokenSource = new CancellationTokenSource();
			_systemData = DataUtil.GetSystemData();
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
			Log.Debug($"Loaded {_sDevices.Length} devices.");
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

				await dev.ReloadData().ConfigureAwait(false);
				if (DeviceMode != DeviceMode.Off && dev.Data.Enable && !dev.Streaming) {
					await dev.StartStream(_targetTokenSource.Token).ConfigureAwait(false);
				}

				if (DeviceMode == DeviceMode.Off || dev.Data.Enable || !dev.Streaming) {
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
					string tag = c.Replace("Glimmr.Models.ColorTarget.", "");
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
			if (init) Log.Debug("Initializing mode.");
			DeviceMode = newMode;
			// Don't unset auto-disable if init is set...
			if (_autoDisabled) {
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
			IColorSource? stream = null;
			if (newMode == DeviceMode.Udp) {
				stream = (StreamMode) sd.StreamMode == StreamMode.DreamScreen
					? _streams["DreamScreen"]
					: _streams["UDP"];
			} else if (newMode != DeviceMode.Off) {
				stream = _streams[newMode.ToString()];
			}
			
			_stream = stream;

			if (stream != null) {
				Log.Information("Starting stream on " + newMode);
				_streamTask = stream.ToggleStream(_streamTokenSource.Token);
				_stream = stream;
			} else {
				Log.Warning("Unable to acquire stream.");
			}

			if (newMode != 0 && !_streamStarted && !_autoDisabled && !init) {
				await StartStream();
			}

			DeviceMode = newMode;
			Log.Information($"Device mode updated to {newMode}.");
		}

		private Task StartStream() {
			if (!_streamStarted) {
				_streamStarted = true;
				Log.Information("Starting streaming targets...");
				foreach (var sDev in _sDevices) {
					try {
						if (sDev.Enable) {
							sDev.StartStream(_targetTokenSource.Token);
						}
					} catch (Exception e) {
						Log.Warning("Exception starting stream: " + e.Message);
					}
				}

				_streamStarted = true;
			}

			if (_streamStarted) {
				Log.Information("Streaming started on all devices.");
			}

			return Task.CompletedTask;
		}

		private Task StopStream() {
			if (!_streamStarted) {
				return Task.CompletedTask;
			}

			Log.Information("Stopping device stream(s)...");
			_streamStarted = false;
			foreach (var dev in _sDevices) {
				if (dev.Enable) {
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

			if (!_sending) {
				_sending = true;
				if (ColorSendEventAsync != null) {
					await ColorSendEventAsync
						.InvokeAsync(this, new ColorSendEventArgs(colors, sectors, fadeTime, force));
					Counter.Tick("source");
				}
				_sending = false;
			} else {
				Log.Debug("Skip send.");
			}
			await Task.FromResult(true);
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
	}
	public class ColorSendEventArgs : EventArgs
	{
		public Color[] LedColors { get; set; }
		public Color[] SectorColors { get; set; }
		public int FadeTime { get; set; }
		public bool Force { get; set; }

		public ColorSendEventArgs(Color[] leds, Color[] sectors, int fadeTime, bool force) {
			LedColors = leds;
			SectorColors = sectors;
			FadeTime = fadeTime;
			Force = force;
		}
	}
}