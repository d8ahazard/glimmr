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
		private HttpClient _nanoClient;
		private Socket _nanoSocket;
		
		private Dictionary<string, int> _subscribers;

		private List<IStreamingDevice> _sDevices;

		private int _captureMode;
		private int _deviceMode;
		private int _deviceGroup;
		private int _ambientMode;
		private int _ambientShow;
		private string _ambientColor;
		private bool _testingStrip;
		private LedData _ledData;
		private CancellationToken _stopToken;
		private Stopwatch _watch;

		

		public ColorService(ControlService controlService) {
			_controlService = controlService;
			_controlService.TriggerSendColorsEvent += SendColors;
			_controlService.SetModeEvent += Mode;
			_controlService.DeviceReloadEvent += RefreshDeviceData;
			_controlService.RefreshLedEvent += ReloadLedData;
			_controlService.TestLedEvent += LedTest;
			_sDevices = new List<IStreamingDevice>();
			LogUtil.Write("Initialization complete.");
		}

		protected override Task ExecuteAsync(CancellationToken stoppingToken) {
			_stopToken = stoppingToken;
			LogUtil.Write("Starting colorService loop...");
			_subscribers = new Dictionary<string, int>();
			_controlService.DeviceReloadEvent += RefreshDeviceData;
			_controlService.SetModeEvent += Mode;
			_watch = new Stopwatch();
			LoadData();
			// Fire da demo
			Demo();
			LogUtil.Write("Starting capture service...");
			LogUtil.Write("Starting video capture task...");
			StartVideoStream(stoppingToken);
			LogUtil.Write("Starting audio capture task...");
			StartAudioStream(stoppingToken);
			LogUtil.Write("Color service tasks started, setting Mode...");
			StartAmbientStream(stoppingToken);
			// Start our initial mode
			Mode(_deviceMode);
			LogUtil.Write("All color service tasks started, really executing main loop...");

			return Task.Run(async () => {
				// If our control service says so, refresh data on all devices while streaming
				LogUtil.Write("Control service starting...");
				while (!stoppingToken.IsCancellationRequested) {
					CheckAutoDisable();
					CheckSubscribers();
					await Task.Delay(1, stoppingToken);
				}

				StopServices();
				LogUtil.Write("Color service stopped.");
				return Task.CompletedTask;
			});
		}

		private void CheckAutoDisable() {
			if (_videoStream == null) return;
			if (_videoStream.SourceActive) {
				_watch.Reset();
				if (!_autoDisabled) return;
				_autoDisabled = false;
				LogUtil.Write("Auto-enabling stream.");
				_controlService.SetMode(_deviceMode);
			} else {
				if (_autoDisabled) return;
				if (_deviceMode != 1) return;
				if (!_watch.IsRunning) _watch.Start();
				if (_watch.ElapsedMilliseconds > 5000) {
					LogUtil.Write("Auto-sleeping lights.");
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
				DreamUtil.SendUdpWrite(0x01, 0x0C, new byte[] {0x01}, 0x30, (byte) _deviceGroup, null, true);
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

		private void LedTest(int len, bool stop, int test) {
			_testingStrip = stop;
			if (stop)
				_strip.StopTest();
			else
				_strip.StartTest(len, test);
		}

		private void LoadData() {
			LogUtil.Write("Loading device data...");
			// Reload main vars
			_captureMode = DataUtil.GetItem<int>("CaptureMode") ?? 2;
			_deviceMode = DataUtil.GetItem<int>("DeviceMode") ?? 0;
			_deviceGroup = DataUtil.GetItem<int>("DeviceGroup") ?? 0;
			_ambientMode = DataUtil.GetItem<int>("AmbientMode") ?? 0;
			_ambientShow = DataUtil.GetItem<int>("AmbientShow") ?? 0;
			_ambientColor = DataUtil.GetItem<string>("AmbientColor") ?? "FFFFFF";
			_sendTokenSource = new CancellationTokenSource();
			_captureTokenSource = new CancellationTokenSource();
			LogUtil.Write("Loading strip");
			_ledData = DataUtil.GetObject<LedData>("LedData");
			try {
				_strip = new LedStrip(_ledData);
				LogUtil.Write("Initialized LED strip...");
			} catch (TypeInitializationException e) {
				LogUtil.Write("Type init error: " + e.Message);
			}

			LogUtil.Write("Creating new device lists...");
			// Create new lists
			_sDevices = new List<IStreamingDevice>();

			var bridgeArray = DataUtil.GetCollection<HueData>("Dev_Hue");
			foreach (var bridge in bridgeArray.Where(bridge =>
				!string.IsNullOrEmpty(bridge.Key) && !string.IsNullOrEmpty(bridge.User) &&
				bridge.SelectedGroup != "-1")) {
				LogUtil.Write("Adding Hue device: " + bridge.Id);
				_sDevices.Add(new HueBridge(bridge));
			}

			// Init leaves
			var leaves = DataUtil.GetCollection<NanoleafData>("Dev_Nanoleaf");
			foreach (var n in leaves.Where(n => !string.IsNullOrEmpty(n.Token) && n.Layout != null)) {
				_nanoClient ??= new HttpClient();

				// Init nano socket
				if (_nanoSocket == null) {
					_nanoSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
					_nanoSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
					_nanoSocket.EnableBroadcast = false;
					LogUtil.Write("Adding Nano device: " + n.Id);
				}

				_sDevices.Add(new NanoleafDevice(n, _nanoSocket, _nanoClient));
			}

			// Init lifx
			var lifx = DataUtil.GetCollection<LifxData>("Dev_Lifx");
			if (lifx != null)
				foreach (var b in lifx.Where(b => b.TargetSector != -1)) {
					_lifxClient ??= LifxClient.CreateAsync().Result;
					LogUtil.Write("Adding Lifx device: " + b.Id);
					_sDevices.Add(new LifxDevice(b, _lifxClient));
				}

			var wlArray = DataUtil.GetCollection<WledData>("Dev_Wled");
			foreach (var wl in wlArray) {
				LogUtil.Write("Adding Wled device: " + wl.Id);
				_sDevices.Add(new WledDevice(wl));
			}

			LogUtil.Write("Initializing Splitter.");
			LogUtil.Write("Color Service Data Load Complete...");
		}

		private void Demo() {
			LogUtil.Write("Demo fired...");
			var ledCount = _ledData.LedCount;
			LogUtil.Write("Running demo on " + ledCount + "Leds");
			var i = 0;
			var cols = new Color[ledCount];
			cols = ColorUtil.EmptyColors(cols);
			var dcs = new CancellationTokenSource();
			var wlDict = new List<WledDevice>();
			var wlCols = new Dictionary<string, Color[]>();
			LogUtil.Write("Still alive 1, we have " + _sDevices.Count + " streaming devices.");
			foreach (var sd in _sDevices) {
				var id = sd.Id;
				LogUtil.Write("Looping sdevice: " + id);
				if (string.IsNullOrEmpty(id)) continue;
				if (!sd.Id.Contains("wled")) continue;
				LogUtil.Write("Got a wled...");
				if (!sd.IsEnabled()) continue;
				LogUtil.Write("And it's enabled.");
				sd.StartStream(dcs.Token);
				LogUtil.Write("Started stream, netx.");
				var wlStrip = (WledDevice) sd;
				wlDict.Add(wlStrip);
				var wlCount = wlStrip.Data.LedCount;
				wlCols[sd.Id] = ColorUtil.EmptyColors(new Color[wlCount]);
				LogUtil.Write("And saved and stuff...");
			}

			while (i < ledCount) {
				var pi = i * 1.0f;
				var progress = pi / ledCount;
				var rCol = ColorUtil.Rainbow(progress);
				// Update our next pixel on the strip with the rainbow color
				cols[i] = rCol;
				_strip.UpdateAll(cols.ToList());
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
			_strip.StopLights();
			dcs.Dispose();
		}


		private AudioStream GetStream() {
			try {
				return new AudioStream(this, _stopToken);
			} catch (DllNotFoundException e) {
				LogUtil.Write("Unable to load bass Dll: " + e.Message);
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
				LogUtil.Write("Restarting streaming device.");
				sd.StartStream(_sendTokenSource.Token);
			}

			if (exists) return;
			var dev = DataUtil.GetDeviceById(id);
			IStreamingDevice? sda = dev.Tag switch {
				"Lifx" => new LifxDevice(dev, _lifxClient),
				"HueBridge" => new HueBridge(dev),
				"Nanoleaf" => new NanoleafDevice(dev),
				"Wled" => new WledDevice(dev),
				"DreamData" => new DreamDevice(dev),
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
			_ambientColor = DataUtil.GetItem<string>("AmbientColor") ?? "FFFFFF";
			LedData ledData = DataUtil.GetObject<LedData>("LedData") ?? new LedData(true);
			try {
				_strip?.Reload(ledData);
				LogUtil.Write("Re-Initialized LED strip...");
			} catch (TypeInitializationException e) {
				LogUtil.Write("Type init error: " + e.Message);
			}
		}

		private void Mode(int newMode) {
			LogUtil.Write("We are updating mode to: " + newMode);
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
					_videoStream?.ToggleSend();
					_audioStream?.ToggleSend(false);
					if (_captureMode == 0) _controlService.TriggerDreamSubscribe();
					break;
				case 2: // Audio
					if (!_streamStarted) StartStream();
					_videoStream?.ToggleSend(false);
					_audioStream?.ToggleSend();
					if (_captureMode == 0) _controlService.TriggerDreamSubscribe();
					break;
				case 3: // Ambient
					if (!_streamStarted) StartStream();
					_videoStream?.ToggleSend(false);
					_audioStream?.ToggleSend(false);
					break;
			}
		}

		
		private void StartVideoStream(CancellationToken ct) {
			if (_captureMode == 0) {
				_controlService.TriggerDreamSubscribe();
			} else {
				LogUtil.Write("Initializing video stream...");
				LogUtil.Write("Continuing...");
				_videoStream = new VideoStream(this, ct);
				if (_videoStream == null) {
					Log.Warning("Video stream is null.");
					return;
				}
				LogUtil.Write("Setting video capture task...");
				Task.Run(async () => _videoStream.Initialize(), ct);
				LogUtil.Write("Starting video capture task...");
				//_videoCaptureTask.Start();
				LogUtil.Write("Video capture task has been started.");
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

				LogUtil.Write("Starting audio capture task...");
				Task.Run(async () => _audioStream.Initialize(), ct);
				LogUtil.Write("Audio capture task has been started.");
			}
		}
		
		private void StartAmbientStream(CancellationToken ct) {
			_ambientStream = new AmbientStream(this, ct); 
			LogUtil.Write("Starting ambient show builder...");
			Task.Run(async () => _ambientStream.Initialize(), ct);
			LogUtil.Write("Audio capture task has been started.");
			
		}

		private void StartStream() {
			if (!_streamStarted) {
				LogUtil.Write("Starting stream...");
				foreach (var sd in _sDevices.Where(sd => !sd.Streaming)) {
					if (!sd.IsEnabled()) continue;
					LogUtil.Write("Starting device: " + sd.IpAddress);
					sd.StartStream(_sendTokenSource.Token);
					LogUtil.Write($"Started device at {sd.IpAddress}.");
				}
			}

			_streamStarted = true;
			if (_streamStarted) LogUtil.Write("Streaming on all devices should now be started...");
		}

		private void StopStream() {
			if (!_streamStarted) return;
			_videoStream?.ToggleSend(false);
			_audioStream?.ToggleSend(false);
			_strip?.StopLights();
			foreach (var s in _sDevices.Where(s => s.Streaming)) s.StopStream();

			LogUtil.Write("Stream stopped.");
			_streamStarted = false;
		}

	

		public void SendColors(List<Color> colors, List<Color> sectors, int fadeTime = 0) {
			LogUtil.Write("SEND FIRED.");
			_sendTokenSource ??= new CancellationTokenSource();
			if (_sendTokenSource.IsCancellationRequested) return;
			if (!_streamStarted) return;
			if (!_testingStrip) _strip?.UpdateAll(colors);

			foreach (var sd in _sDevices.Where(sd => sd.IsEnabled())) {
				//LogUtil.Write("SD: " + sd.Id + " enabled");
				if (sd.Id.Contains("wled") || sd.Id.Contains("Wled")) {
					sd.SetColor(colors, null, fadeTime);
				} else {
					LogUtil.Write("Setting colors for non wled...");
					sd.SetColor(null, sectors, fadeTime);
				}
			}
			// We call this so we don't create some dumb loop
			_controlService.SendColors2(colors, sectors, fadeTime);
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

			LogUtil.Write("All services have been stopped.");
		}
	}
}