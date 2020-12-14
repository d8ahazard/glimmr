#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Hubs;
using Glimmr.Models.ColorSource.Ambient;
using Glimmr.Models.ColorSource.Audio;
using Glimmr.Models.ColorSource.Video;
using Glimmr.Models.ColorSource.Video.Stream;
using Glimmr.Models.LED;
using Glimmr.Models.StreamingDevice;
using Glimmr.Models.StreamingDevice.Dreamscreen;
using Glimmr.Models.StreamingDevice.Hue;
using Glimmr.Models.StreamingDevice.LIFX;
using Glimmr.Models.StreamingDevice.Nanoleaf;
using Glimmr.Models.StreamingDevice.WLED;
using Glimmr.Models.Util;
using LifxNet;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Serilog;
using Color = System.Drawing.Color;

#endregion

namespace Glimmr.Services {
	// Handles capturing and sending color data
	public class ColorService : BackgroundService {
		private readonly ControlService _controlService;
		private LedStrip _strip;
		private VideoStream _videoStream;
		private AudioStream _audioStream;
		private AmbientStream _ambientStream;
		private bool _autoDisabled;
		private bool _streamStarted;
		private CancellationTokenSource _captureTokenSource;
		private CancellationTokenSource _sendTokenSource;

		// Figure out how to make these generic, non-callable
		private LifxClient _lifxClient;
		
		private Dictionary<string, int> _subscribers;

		private List<IStreamingDevice> _sDevices;

		private int _captureMode;
		private int _deviceMode;
		private int _deviceGroup;
		private int _ambientMode;
		private int _ambientShow;
		private Color _ambientColor;
		private bool _testingStrip;
		private LedData _ledData;
		private CancellationToken _stopToken;
		private Stopwatch _watch;
		private DreamUtil _dreamUtil;

		

		public ColorService(ControlService controlService) {
			_controlService = controlService;
			_controlService.TriggerSendColorsEvent += SendColors;
			_controlService.SetModeEvent += Mode;
			_controlService.DeviceReloadEvent += RefreshDeviceData;
			_controlService.RefreshLedEvent += ReloadLedData;
			_controlService.TestLedEvent += LedTest;
			_controlService.AddSubscriberEvent += AddSubscriber;
			_sDevices = new List<IStreamingDevice>();
			_dreamUtil = new DreamUtil(_controlService.UnicastSender, _controlService.BroadcastSender);
			Log.Debug("Initialization complete.");
		}

		protected override Task ExecuteAsync(CancellationToken stoppingToken) {
			_stopToken = stoppingToken;
			Log.Information("Starting colorService loop...");
			_subscribers = new Dictionary<string, int>();
			_controlService.DeviceReloadEvent += RefreshDeviceData;
			_controlService.SetModeEvent += Mode;
			_watch = new Stopwatch();
			LoadData();
			// Fire da demo
			Demo();
			Log.Information("Starting video capture task...");
			StartVideoStream(stoppingToken);
			Log.Information("Done. Starting audio capture task...");
			StartAudioStream(stoppingToken);
			Log.Information("Done. Starting ambient builder task...");
			StartAmbientStream(stoppingToken);
			Log.Information("All color sources initialized, setting mode.");
			// Start our initial mode
			Mode(_deviceMode);
			Log.Information("All color services have been initialized.");

			return Task.Run(async () => {
				while (!stoppingToken.IsCancellationRequested) {
					CheckAutoDisable();
					CheckSubscribers();
					await Task.Delay(1, stoppingToken);
				}

				StopServices();
				Log.Debug("Color service stopped.");
				return Task.CompletedTask;
			});
		}

		private void CheckAutoDisable() {
			if (_videoStream == null) return;
			if (_videoStream.SourceActive) {
				_watch.Reset();
				if (!_autoDisabled) return;
				_autoDisabled = false;
				Log.Debug("Auto-enabling stream.");
				_controlService.SetMode(_deviceMode);
			} else {
				if (_autoDisabled) return;
				if (_deviceMode != 1) return;
				if (!_watch.IsRunning) _watch.Start();
				if (_watch.ElapsedMilliseconds > 5000) {
					Log.Debug("Auto-sleeping lights.");
					_autoDisabled = true;
					_deviceMode = 0;
					DataUtil.SetItem<bool>("AutoDisabled", _autoDisabled);
					DataUtil.SetItem<bool>("DeviceMode", _deviceMode);
					_controlService.SetMode(_deviceMode);
					_watch.Reset();
				} else {
					if (_watch.ElapsedMilliseconds % 1000 != 0) return;
					_watch.Reset();
				}
			}
			
		}

		private void CheckSubscribers() {
			try {
				// Send our subscribe multicast
				_dreamUtil.SendUdpWrite(0x01, 0x0C, new byte[] {0x01}, 0x30, (byte) _deviceGroup, null, true);
				// Enumerate all subscribers, check to see that they are still valid
				var keys = new List<string>(_subscribers.Keys);
				foreach (var key in keys) {
					// If the subscribers haven't replied in three messages, remove them, otherwise, count down one
					if (_subscribers[key] <= 0) {
						_subscribers.Remove(key);
					} else {
						_subscribers[key] -= 1;
					}
				}
			} catch (TaskCanceledException) {
				_subscribers = new Dictionary<string, int>();
			}
		}

		private void AddSubscriber(string ip) {
			_subscribers[ip] = 3;
		}

		private void LedTest(int len, bool stop, int test) {
			_testingStrip = stop;
			if (stop)
				_strip.StopTest();
			else
				_strip.StartTest(len, test);
		}

		private void LoadData() {
			Log.Debug("Loading device data...");
			// Reload main vars
			_captureMode = DataUtil.GetItem<int>("CaptureMode") ?? 2;
			_deviceMode = DataUtil.GetItem<int>("DeviceMode") ?? 0;
			_deviceGroup = DataUtil.GetItem<int>("DeviceGroup") ?? 0;
			_ambientMode = DataUtil.GetItem<int>("AmbientMode") ?? 0;
			_ambientShow = DataUtil.GetItem<int>("AmbientShow") ?? 0;
			_ambientColor = DataUtil.GetObject<Color>("AmbientColor") ?? Color.FromArgb(255,255,255,255);
			_sendTokenSource = new CancellationTokenSource();
			_captureTokenSource = new CancellationTokenSource();
			Log.Debug("Loading strip");
			_ledData = DataUtil.GetObject<LedData>("LedData");
			try {
				_strip = new LedStrip(_ledData);
				Log.Debug("Initialized LED strip...");
			} catch (TypeInitializationException e) {
				Log.Debug("Type init error: " + e.Message);
			}

			Log.Debug("Creating new device lists...");
			// Create new lists
			_sDevices = new List<IStreamingDevice>();

			var bridgeArray = DataUtil.GetCollection<HueData>("Dev_Hue");
			foreach (var bridge in bridgeArray.Where(bridge =>
				!string.IsNullOrEmpty(bridge.Key) && !string.IsNullOrEmpty(bridge.User) &&
				bridge.SelectedGroup != "-1")) {
				Log.Debug("Adding Hue device: " + bridge.Id);
				_sDevices.Add(new HueDevice(bridge));
			}

			// Init leaves
			var leaves = DataUtil.GetCollection<NanoleafData>("Dev_Nanoleaf");
			foreach (var n in leaves.Where(n => !string.IsNullOrEmpty(n.Token) && n.Layout != null)) {
				_sDevices.Add(new NanoleafDevice(n, _controlService.UnicastSender, _controlService.HttpSender));
			}

			// Init lifx
			var lifx = DataUtil.GetCollection<LifxData>("Dev_Lifx");
			if (lifx != null)
				foreach (var b in lifx.Where(b => b.TargetSector != -1)) {
					_lifxClient ??= LifxClient.CreateAsync().Result;
					Log.Debug("Adding Lifx device: " + b.Id);
					_sDevices.Add(new LifxDevice(b, _lifxClient));
				}

			var wlArray = DataUtil.GetCollection<WledData>("Dev_Wled");
			foreach (var wl in wlArray) {
				Log.Debug("Adding Wled device: " + wl.Id);
				_sDevices.Add(new WledDevice(wl, _controlService.UnicastSender, _controlService.HttpSender));
			}

			Log.Debug("Initializing Splitter.");
			Log.Debug("Color Service Data Load Complete...");
		}

		private void Demo() {
			Log.Debug("Demo fired...");
			var ledCount = _ledData.LedCount;
			Log.Debug("Running demo on " + ledCount + "pixels");
			var i = 0;
			var cols = new Color[ledCount];
			cols = ColorUtil.EmptyColors(cols);
			var dcs = new CancellationTokenSource();
			var wlDict = new List<WledDevice>();
			var wlCols = new Dictionary<string, Color[]>();
			Log.Debug("Still alive 1, we have " + _sDevices.Count + " streaming devices.");
			foreach (var sd in from sd in _sDevices let id = sd.Id where !string.IsNullOrEmpty(id) where sd.Id.Contains("wled") where sd.IsEnabled() select sd) {
				sd.StartStream(dcs.Token);
				var wlStrip = (WledDevice) sd;
				wlDict.Add(wlStrip);
				var wlCount = wlStrip.Data.LedCount;
				wlCols[sd.Id] = ColorUtil.EmptyColors(new Color[wlCount]);
			}

			while (i < ledCount) {
				var pi = i * 1.0f;
				var progress = pi / ledCount;
				var rCol = ColorUtil.Rainbow(progress);
				// Update our next pixel on the strip with the rainbow color
				cols[i] = rCol;
				_strip?.UpdateAll(cols.ToList());
				// Go through each device in our list of wled devices
				foreach (var w in wlDict) {
					// This is the value the color should go at in our array of possible colors
					var j = i - w.Data.Offset - 1;
					// This is the final pixel in our list
					var k = w.Data.Offset + w.Data.LedCount - 1;

					// Otherwise, check to see if j is within our LED range
					if (j >= 0) {
						if (j < k && j < wlCols[w.Id].Length) wlCols[w.Id][j] = rCol;
						if (k > ledCount) {
							// If so, calculate the value to the start of the full array
							var l = k - ledCount;
							// If this new value is within, grab it assign it to our array
							if (i == l) wlCols[w.Id][j] = rCol;
						}
					}

					// RENDER IT
					w.SetColor(wlCols[w.Id].ToList(), null, 0);
				}

				i++;
				Thread.Sleep(2);
			}

			// Finally show off our hard work
			Thread.Sleep(500);

			// THEN RIP IT ALL DOWN
			foreach (var w in wlDict) w.StopStream();
			_strip?.StopLights();
			dcs.Dispose();
		}


		private AudioStream GetStream() {
			try {
				return new AudioStream(this, _stopToken);
			} catch (DllNotFoundException e) {
				Log.Warning("Unable to load bass Dll:", e);
			}
			return null;
		}


		private void RefreshDeviceData(string id) {
			var exists = false;
			foreach (var sd in _sDevices.Where(sd => sd.Id == id)) {
				sd.StopStream();
				sd.ReloadData();
				exists = true;
				if (!sd.IsEnabled()) continue;
				Log.Debug("Restarting streaming device.");
				sd.StartStream(_sendTokenSource.Token);
			}

			if (exists) return;
			var dev = DataUtil.GetDeviceById(id);
			IStreamingDevice sda = dev.Tag switch {
				"Lifx" => new LifxDevice(dev, _lifxClient),
				"HueBridge" => new HueDevice(dev),
				"Nanoleaf" => new NanoleafDevice(dev),
				"Wled" => new WledDevice(dev, _controlService.UnicastSender, _controlService.HttpSender),
				"DreamData" => new DreamDevice(dev, _dreamUtil),
				_ => null
			};

			// If our device is a real boy, start it and add it
			if (sda == null) return;
			sda.StartStream(_sendTokenSource.Token);
			_sDevices.Add(sda);
		}

		private void ReloadLedData() {
			_captureMode = DataUtil.GetItem<int>("CaptureMode") ?? 2;
			_deviceMode = DataUtil.GetItem<int>("DeviceMode") ?? 0;
			_deviceGroup = DataUtil.GetItem<int>("DeviceGroup") ?? 0;
			_ambientMode = DataUtil.GetItem<int>("AmbientMode") ?? 0;
			_ambientShow = DataUtil.GetItem<int>("AmbientShow") ?? 0;
			_ambientColor = DataUtil.GetItem<Color>("AmbientColor") ?? Color.FromArgb(255,255,255,255);
			LedData ledData = DataUtil.GetObject<LedData>("LedData") ?? new LedData();
			try {
				_strip?.Reload(ledData);
				Log.Debug("Re-Initialized LED strip...");
			} catch (TypeInitializationException e) {
				Log.Debug("Type init error: " + e.Message);
			}
		}

		private void Mode(int newMode) {
			if (newMode != 0 && _autoDisabled) {
				_autoDisabled = false;
				DataUtil.SetItem<bool>("AutoDisabled", _autoDisabled);
			}

			switch (newMode) {
				case 0:
					if (_streamStarted) StopStream();
					break;
				case 1:
					if (!_streamStarted) StartStream();
					_audioStream?.ToggleSend(false);
					_ambientStream.ToggleSend(false);
					_videoStream?.ToggleSend();
					if (_captureMode == 0) _controlService.TriggerDreamSubscribe();
					break;
				case 2: // Audio
					if (!_streamStarted) StartStream();
					_videoStream?.ToggleSend(false);
					_ambientStream?.ToggleSend(false);
					_audioStream?.ToggleSend();
					if (_captureMode == 0) _controlService.TriggerDreamSubscribe();
					break;
				case 3: // Ambient
					if (!_streamStarted) StartStream();
					_videoStream?.ToggleSend(false);
					_audioStream?.ToggleSend(false);
					_ambientStream?.ToggleSend();
					break;
			}
			Log.Information($"Updating device mode to {newMode}.");
		}

		
		private void StartVideoStream(CancellationToken ct) {
			if (_captureMode == 0) {
				_controlService.TriggerDreamSubscribe();
			} else {
				_videoStream = new VideoStream(this, ct);
				if (_videoStream == null) {
					Log.Warning("Video stream is null.");
					return;
				}
				Task.Run(() => _videoStream.Initialize(), ct);
			}
		}


		private void StartAudioStream(CancellationToken ct) {
			if (_captureMode == 0) {
				_controlService.TriggerDreamSubscribe();
			} else {
				_audioStream = GetStream(); 
				if (_audioStream == null) {
					Log.Warning("Audio stream is null.");
					return;
				}
				Task.Run( () => _audioStream.Initialize(), ct);
			}
		}
		
		private void StartAmbientStream(CancellationToken ct) {
			_ambientStream = new AmbientStream(this, ct); 
			Task.Run( () => _ambientStream.Initialize(), ct);
		}

		private void StartStream() {
			if (!_streamStarted) {
				foreach (var sd in _sDevices.Where(sd => !sd.Streaming)) {
					if (!sd.IsEnabled()) continue;
					sd.StartStream(_sendTokenSource.Token);
				}
			}

			_streamStarted = true;
			if (_streamStarted) Log.Information("Streaming on all devices should now be started...");
		}

		private void StopStream() {
			if (!_streamStarted) return;
			_videoStream?.ToggleSend(false);
			_audioStream?.ToggleSend(false);
			_strip?.StopLights();
			foreach (var s in _sDevices.Where(s => s.Streaming)) s.StopStream();

			Log.Information("Stream stopped.");
			_streamStarted = false;
		}

	

		public void SendColors(List<Color> colors, List<Color> sectors, int fadeTime = 0) {
			_sendTokenSource ??= new CancellationTokenSource();
			if (_sendTokenSource.IsCancellationRequested) return;
			if (!_streamStarted) return;
			if (!_testingStrip) _strip?.UpdateAll(colors);

			foreach (var sd in _sDevices.Where(sd => sd.IsEnabled())) {
				if (sd.Id.Contains("wled") || sd.Id.Contains("Wled")) {
					sd.SetColor(colors, null, fadeTime);
				} else {
					sd.SetColor(null, sectors, fadeTime);
				}
			}
		}

		
		private static void CancelSource(CancellationTokenSource target, bool dispose = false) {
			if (target == null) return;
			if (!target.IsCancellationRequested) target.CancelAfter(0);

			if (dispose) target.Dispose();
		}

		private void StopServices() {
			CancelSource(_captureTokenSource, true);
			Thread.Sleep(500);
			_strip?.StopLights();
			_strip?.Dispose();
			foreach (var s in _sDevices) {
				s.StopStream();
				s.Dispose();
			}

			Log.Information("All services have been stopped.");
		}
	}
}