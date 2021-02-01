#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models;
using Glimmr.Models.ColorSource.Ambient;
using Glimmr.Models.ColorSource.Audio;
using Glimmr.Models.ColorSource.AudioVideo;
using Glimmr.Models.ColorSource.Video;
using Glimmr.Models.ColorTarget;
using Glimmr.Models.ColorTarget.DreamScreen;
using Glimmr.Models.ColorTarget.Hue;
using Glimmr.Models.ColorTarget.LED;
using Glimmr.Models.ColorTarget.LIFX;
using Glimmr.Models.ColorTarget.Nanoleaf;
using Glimmr.Models.ColorTarget.Wled;
using Glimmr.Models.ColorTarget.Yeelight;
using Glimmr.Models.Util;
using LifxNet;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Serilog;
using Color = System.Drawing.Color;

// ReSharper disable All

#endregion

namespace Glimmr.Services {
	// Handles capturing and sending color data
	public class ColorService : BackgroundService {
		private readonly ControlService _controlService;
		private readonly DreamUtil _dreamUtil;
		private AmbientStream _ambientStream;
		private AudioStream _audioStream;
		private CancellationTokenSource _captureTokenSource;
		private bool _autoDisabled;
		private int _captureMode;
		private int _deviceGroup;
		private int _deviceMode;
		private SystemData _systemData;

		// Figure out how to make these generic, non-callable
		private LifxClient _lifxClient;

		private IStreamingDevice[] _sDevices;
		
		private CancellationTokenSource _sendTokenSource;
		private CancellationTokenSource _streamTokenSource;
		private CancellationToken _stopToken;
		private bool _streamStarted;
		
		private Dictionary<string, int> _subscribers;
		private VideoStream _videoStream;
		private AudioVideoStream _avStream;
		private Task _streamTask;

		public AsyncEvent<DynamicEventArgs> ColorSendEvent;
		public event Action<List<Color>, string> SegmentTestEvent = delegate { };

		public ColorService(ControlService controlService) {
			_controlService = controlService;
			_controlService.TriggerSendColorEvent += SendColors;
			_controlService.SetModeEvent += Mode;
			_controlService.DeviceReloadEvent += RefreshDeviceData;
			_controlService.RefreshLedEvent += ReloadLedData;
			_controlService.RefreshSystemEvent += ReloadSystemData;
			_controlService.TestLedEvent += LedTest;
			_controlService.AddSubscriberEvent += AddSubscriber;
			_controlService.FlashDeviceEvent += FlashDevice;
			_controlService.FlashSectorEvent += FlashSector;
			_controlService.DemoLedEvent += Demo;
			_dreamUtil = new DreamUtil(_controlService.UdpClient);
			Log.Debug("Initialization complete.");
		}
		
		protected override Task ExecuteAsync(CancellationToken stoppingToken) {
			_stopToken = stoppingToken;
			_streamTokenSource = new CancellationTokenSource();
			Log.Information("Starting colorService loop...");
			_subscribers = new Dictionary<string, int>();
			LoadData();
			_ambientStream = new AmbientStream(this,stoppingToken);
			_videoStream = new VideoStream(this,_controlService,stoppingToken);
			_audioStream = GetStream(stoppingToken);
			_avStream = new AudioVideoStream(this, _audioStream, _videoStream);
			
			Log.Information("All color services have been initialized.");
			return Task.Run(async () => {
				Log.Information("Starting video capture task...");
				// Start our initial mode
				await Demo(this, new DynamicEventArgs());
				Log.Information($"All color sources initialized, setting mode to {_deviceMode}.");
				await Mode(this, new DynamicEventArgs(_deviceMode)).ConfigureAwait(true);

				while (!stoppingToken.IsCancellationRequested) {
					await CheckAutoDisable();
					await CheckSubscribers();
					await Task.Delay(5000, stoppingToken);
				}
			}, CancellationToken.None);
		}

		public override async Task StopAsync(CancellationToken cancellationToken) {
			Log.Debug("Stopping color service...");
			await StopServices();
			DataUtil.Dispose();
			Log.Debug("Color service stopped.");
			await base.StopAsync(cancellationToken);
		}

		private Task FlashDevice(object o, DynamicEventArgs dynamicEventArgs) {
			var devId = dynamicEventArgs.P1;
			var disable = false;
			var ts = new CancellationTokenSource();
			var lc = 300;
			try {
				lc = _systemData.LedCount;
			} catch (Exception) {
				
			}

			var bColor = Color.FromArgb(0, 0, 0, 0);
			var rColor = Color.FromArgb(255, 255, 0, 0);
			for (var i = 0; i < _sDevices.Length; i++) {
				if (_sDevices[i].Id == devId) {
					var sd = _sDevices[i];
					sd.Testing = true;
					Log.Debug("Flashing device: " + devId);
					if (!sd.Streaming) {
						disable = true;
						sd.StartStream(ts.Token);
					}
					sd.FlashColor(rColor);
					Thread.Sleep(500);
					sd.FlashColor(bColor);
					Thread.Sleep(500);
					sd.FlashColor(rColor);
					Thread.Sleep(500);
					sd.FlashColor(bColor);
					sd.Testing = false;
					if (!disable) {
						continue;
					}

					sd.StopStream();
					ts.Cancel();
					ts.Dispose();
				}
			}

			return Task.CompletedTask;
		}

		private async Task FlashSector(object o, DynamicEventArgs dynamicEventArgs) {
			var sector = dynamicEventArgs.P1;
			Log.Debug("No, really, flashing sector: " + sector);
			var col = Color.FromArgb(255, 255, 0, 0);
			var colors = ColorUtil.AddLedColor(new Color[_systemData.LedCount],sector, col,_systemData);
			var black = ColorUtil.EmptyColors(new Color[_systemData.LedCount]);
			var strips = new List<LedDevice>();
			for (var i = 0; i < _sDevices.Length; i++) {
				if (_sDevices[i].Tag == "Led") {
					strips.Add((LedDevice)_sDevices[i]);
				}
			}
			foreach (var _strip in strips) {
				if (_strip != null) {
					_strip.Testing = true;
				}
			}
			
			foreach (var _strip in strips) {
				if (_strip != null) {
					await _strip.SetColor(colors.ToList(), true);
				}
			}

			Thread.Sleep(500);
			
			foreach (var _strip in strips) {
				if (_strip != null) {
					_strip.SetColor(black.ToList(), true);
				}
			}
			
			foreach (var _strip in strips) {
				if (_strip != null) {
					_strip.SetColor(colors.ToList(), true);
				}
			}
			
			Thread.Sleep(1000);
			
			foreach (var _strip in strips) {
				if (_strip != null) {
					_strip.SetColor(black.ToList(), true);
					_strip.Testing = false;
				}
			}
		}
	
		private async Task CheckAutoDisable() {
			var sourceActive = false;
			// If we're in video or audio mode, check the source is active...
			switch (_deviceMode) {
				case 0:
				case 3:
				case 4:
				case 5:
				case 2:
				case 1 when _videoStream == null:
					return;
				case 1:
					sourceActive = _videoStream.SourceActive;
					break;
			}
			
			if (sourceActive) {
				if (!_autoDisabled) return;
				Log.Debug("Auto-enabling stream.");
				_autoDisabled = false;
				DataUtil.SetItem("AutoDisabled", _autoDisabled);
				_controlService.SetModeEvent -= Mode;
				await _controlService.SetMode(_deviceMode);
				_controlService.SetModeEvent += Mode;
			} else {
				if (_autoDisabled || _deviceMode == 2) return;
				Log.Debug("Auto-disabling stream.");
				_autoDisabled = true;
				DataUtil.SetItem("AutoDisabled", _autoDisabled);
				_controlService.SetModeEvent -= Mode;
				await _controlService.SetMode(0);
				_controlService.SetModeEvent += Mode;
			}
		}


		private async Task CheckSubscribers() {
			try {
				await _dreamUtil.SendBroadcastMessage(_deviceGroup);
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
			if (!_subscribers.ContainsKey(ip)) {
				foreach (var t in _sDevices.Where(t => t.Id == ip)) {
					t.Enable = true;
				}

				Log.Debug("ADDING SUBSCRIBER: " + ip);
			}
			_subscribers[ip] = 3;
		}

		private async Task LedTest(object o, DynamicEventArgs dynamicEventArgs) {
			var led = dynamicEventArgs.P1;
			var strips = new List<LedDevice>();
			for (var i = 0; i < _sDevices.Length; i++) {
				if (_sDevices[i].Tag == "Led") {
					strips.Add((LedDevice)_sDevices[i]);
				}
			}
			foreach (var _strip in strips) {
				await _strip.StartTest(led);	
			}
		}

		private void LoadData() {
			Log.Debug("Loading device data...");
			// Reload main vars
			var dev = DataUtil.GetDeviceData();
			_deviceMode = DataUtil.GetItem("DeviceMode");
			_deviceGroup = (byte) dev.DeviceGroup;
			_captureMode = DataUtil.GetItem<int>("CaptureMode") ?? 2;
			_sendTokenSource = new CancellationTokenSource();
			_captureTokenSource = new CancellationTokenSource();
			Log.Debug("Loading strip");
			_systemData = DataUtil.GetObject<SystemData>("SystemData");
			Log.Debug("Creating new device lists...");
			// Create new lists
			var sDevs = new List<IStreamingDevice>();
			
			// Init leaves
			var leaves = DataUtil.GetCollection<NanoleafData>("Dev_Nanoleaf");
			foreach (var n in leaves.Where(n => !string.IsNullOrEmpty(n.Token) && n.Layout != null)) {
				sDevs.Add(new NanoleafDevice(n, _controlService.UdpClient, _controlService.HttpSender, this));
			}

			var dsDevs = DataUtil.GetCollection<DreamData>("Dev_Dreamscreen");
			foreach (var ds in dsDevs) {
				sDevs.Add(new DreamDevice(ds, _dreamUtil, this));
			}

			// Init lifx
			var lifx = DataUtil.GetCollection<LifxData>("Dev_Lifx");
			if (lifx != null) {
				foreach (var b in lifx.Where(b => b.TargetSector != -1)) {
					_lifxClient ??= LifxClient.CreateAsync().Result;
					sDevs.Add(new LifxDevice(b, _lifxClient, this));
				}
			}

			var wlArray = DataUtil.GetCollection<WledData>("Dev_Wled");
			foreach (var wl in wlArray) {
				sDevs.Add(new WledDevice(wl, _controlService.UdpClient, _controlService.HttpSender, this));
			}

			var bridgeArray = DataUtil.GetCollection<HueData>("Dev_Hue");
			foreach (var bridge in bridgeArray.Where(bridge =>
				!string.IsNullOrEmpty(bridge.Key) && !string.IsNullOrEmpty(bridge.User) &&
				bridge.SelectedGroup != "-1")) {
				Log.Debug("Adding Hue device: " + bridge.Id);
				sDevs.Add(new HueDevice(bridge));
			}
			
			var yeeArray = DataUtil.GetCollection<YeelightData>("Dev_Yeelight");
			foreach (var yd in yeeArray) {
				sDevs.Add(new YeelightDevice(yd, this));
			}

			var ledData = DataUtil.GetCollectionItem<LedData>("LedData", "0");
			//var ledData2 = DataUtil.GetCollectionItem<LedData>("LedData", "1");
			//var ledData3 = DataUtil.GetCollectionItem<LedData>("LedData", "2");
			Log.Debug("Initialized LED strip: " + JsonConvert.SerializeObject(ledData));
			//_strips.Add(new LedStrip(ledData2, this));
			//_strips.Add(new LedStrip(ledData3,this));
			sDevs.Add(new LedDevice(ledData, this));
			_sDevices = sDevs.ToArray();
		}

		private async Task Demo(object o, DynamicEventArgs dynamicEventArgs) {
			var stripId = dynamicEventArgs.P1;
			if (String.IsNullOrEmpty(stripId)) StartStream();
			Log.Debug("Demo fired...");
			var ledCount = 300;
			try {
				ledCount = _systemData.LedCount;
			} catch (Exception) {
				
			}
			Log.Debug("Running demo on " + ledCount + "total pixels.");
			var i = 0;
			var k = 0;
			var cols = new Color[ledCount];
			var secs = new Color[28];
			cols = ColorUtil.EmptyColors(cols);
			secs = ColorUtil.EmptyColors(secs);
			
			while (i < ledCount) {
				var pi = i * 1.0f;
				var progress = pi / ledCount;
				var rCol = ColorUtil.Rainbow(progress);
				// Update our next pixel on the strip with the rainbow color
				if (progress >= .0357f * k) {
					secs[k] = rCol;
					k++;
				}

				cols[i] = rCol;
				if (String.IsNullOrEmpty(stripId)) {
					await SendColors(this, new DynamicEventArgs(cols.ToList(), secs.ToList()));	
				} else {
					SegmentTestEvent(cols.ToList(), stripId);
				}
				
				i++;
				Thread.Sleep(2);
			}

			// Finally show off our hard work
			Thread.Sleep(500);
		}


		private AudioStream GetStream(CancellationToken ct) {
			try {
				return new AudioStream(this);
			} catch (DllNotFoundException e) {
				Log.Warning("Unable to load bass Dll:", e);
			}

			return null;
		}


		private async Task RefreshDeviceData(object o, DynamicEventArgs dynamicEventArgs) {
			var id = dynamicEventArgs.P1;
			if (string.IsNullOrEmpty(id)) {
				Log.Warning("Can't refresh null device: " + id);
			}

			if (id == IpUtil.GetLocalIpAddress()) {
				Log.Debug("This is our system data...");
				var myDev = DataUtil.GetDeviceData();
				_deviceGroup = myDev.DeviceGroup;
			}

			var exists = false;
			for (var i = 0; i < _sDevices.Length; i++) {
				if (_sDevices[i].Id == id) {
					var sd = _sDevices[i];
					if (sd.Tag != "Nanoleaf") await sd.StopStream();
					await sd.ReloadData();
					if (!sd.IsEnabled()) {
						return;
					}
					exists = true;
					Log.Debug("Restarting streaming device.");
					if (sd.Tag != "Nanoleaf") await sd.StartStream(_sendTokenSource.Token);
					break;
				}
			}
			
			if (exists) {
				return;
			}

			var dev = DataUtil.GetDeviceById(id);
			Log.Debug("Tag: " + dev.Tag);
			IStreamingDevice sda = dev.Tag switch {
				"Lifx" => new LifxDevice(dev, _lifxClient, this),
				"HueBridge" => new HueDevice(dev, this),
				"Nanoleaf" => new NanoleafDevice(dev, _controlService.UdpClient, _controlService.HttpSender, this),
				"Wled" => new WledDevice(dev, _controlService.UdpClient, _controlService.HttpSender, this),
				"Dreamscreen" => new DreamDevice(dev, _dreamUtil, this),
				"Led" => new LedDevice(dev,this),
				null => null,
				_ => null
			};

			// If our device is a real boy, start it and add it
			if (sda == null) {
				return;
			}

			var sDevs = _sDevices.ToList();
			await sda.StartStream(_sendTokenSource.Token);
			sDevs.Add(sda);
			_sDevices = sDevs.ToArray();
		}

		private void ReloadLedData() {
			_captureMode = DataUtil.GetItem<int>("CaptureMode") ?? 2;
			_deviceMode = DataUtil.GetItem<int>("DeviceMode") ?? 0;
			_deviceGroup = DataUtil.GetItem<int>("DeviceGroup") ?? 0;
		}

		private void ReloadSystemData() {
			_videoStream?.Refresh();
			_audioStream?.Refresh();
		}

		private async Task Mode(object o, DynamicEventArgs dynamicEventArgs) {
			int newMode = dynamicEventArgs.P1;
			_deviceMode = newMode;
			if (newMode != 0 && _autoDisabled) {
				_autoDisabled = false;
				DataUtil.SetItem("AutoDisabled", _autoDisabled);
			}
			
			if (_streamStarted && newMode == 0) {
				await StopStream();
			}

			_streamTokenSource?.Cancel();
			if (_streamTask != null && !_streamTask.IsCompleted) {
				try {
					await (_streamTask);
				} catch (TaskCanceledException) {

				} finally {
					_streamTokenSource?.Dispose();
				}
			}

			_streamTokenSource = new CancellationTokenSource();
			
			switch (newMode) {
				case 1:
					StartVideoStream(_streamTokenSource.Token);
					break;
				case 2: // Audio
					StartAudioStream(_streamTokenSource.Token);
					break;
				case 3: // Ambient
					StartAmbientStream(_streamTokenSource.Token);
					break;
				case 4: // A/V mode :D
					StartAvStream(_streamTokenSource.Token);
					break;
				case 5:
					// Nothing to do, this tells the app to get data from DreamService
					break;
			}

			if (newMode != 0 && !_streamStarted) {
				StartStream();
			}
			_deviceMode = newMode;
			Log.Information($"Device mode updated to {newMode}.");
		}


		private void StartVideoStream(CancellationToken ct, bool sendColors = true) {
			if (_captureMode == 0) {
				_controlService.TriggerDreamSubscribe();
			} else {
				Log.Debug("Starting video stream...");
				if (_videoStream != null) _videoStream.SendColors = sendColors;
				_streamTask = Task.Run(() => _videoStream?.StartStream(ct), CancellationToken.None);
				Log.Debug("Video stream started.");
			}
		}


		private void StartAudioStream(CancellationToken ct, bool sendColors = true) {
			if (_captureMode == 0) {
				_controlService.TriggerDreamSubscribe();
			} else {
				Log.Debug("Starting audio stream...");
				if (_audioStream != null) _audioStream.SendColors = sendColors;
				_streamTask = Task.Run(() => _audioStream?.StartStream(ct), CancellationToken.None);
				Log.Debug("Audio stream started.");
			}
		}

		private void StartAvStream(CancellationToken ct) {
			StartAudioStream(ct, false);
			StartVideoStream(ct, false);
			_streamTask = Task.Run(() => _avStream.StartStream(ct), CancellationToken.None);
			Log.Debug("AV Stream started?");
		}

		private void StartAmbientStream(CancellationToken ct) {
			_streamTask = Task.Run(() => _ambientStream.StartStream(ct), CancellationToken.None);
		}

		private void StartStream() {
			if (!_streamStarted) {
				_streamStarted = true;
				Log.Information("Starting streaming devices...");
				for (var i = 0; i < _sDevices.Length; i++) {
					if (_sDevices[i].Enable) {
						_sDevices[i].StartStream(_sendTokenSource.Token);
					}
				}
			} else {
				Log.Debug("Streaming already started.");
			}
			

			if (_streamStarted) {
				Log.Information("Streaming on all devices should now be started...");
			}
		}

		private async Task StopStream() {
			if (!_streamStarted) {
				return;
			}
			_streamStarted = false;
			var streamers = new List<IStreamingDevice>();
			foreach (var s in _sDevices.Where(s => s.Streaming)) {
				streamers.Add(s);
			}
			await Task.WhenAll(streamers.Select(i => {
				try {
					return i.StopStream();
				} catch (Exception e) {
					Log.Warning("Well, this is exceptional: " + e.Message);
					return Task.CompletedTask;
				}
			})).ConfigureAwait(false);
			Log.Information("Stream stopped.");
			
		}


		public Task SendColors(object o, DynamicEventArgs args) {
			//if (o.GetType().FullName != "Glimmr.Models.ColorSource.Video.VideoStream") Log.Debug("Send called by " + o.GetType().FullName);
			_sendTokenSource ??= new CancellationTokenSource();
			if (_sendTokenSource.IsCancellationRequested) {
				Log.Debug("Send token is canceled.");
				return Task.CompletedTask;
			}

			if (!_streamStarted) {
				//Log.Debug("Stream not started.");
				return Task.CompletedTask;
			}
			
			return ColorSendEvent.InvokeAsync(this, new DynamicEventArgs(args.P1, args.P2, args.P3));
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
			Log.Information("Stopping services...");
			CancelSource(_captureTokenSource, true);
			_streamTokenSource?.Cancel();
			if (_streamTask != null && !_streamTask.IsCompleted) {
				try {
					await (_streamTask);
				} catch (TaskCanceledException) {

				} finally {
					_streamTokenSource?.Dispose();
				}
			}
			CancelSource(_sendTokenSource, true);
			foreach (var s in _sDevices) {
				try {
					Log.Information("Stopping device: " + s.Id);
					await s.StopStream();
					s.Dispose();
				} catch (Exception e) {
					Log.Warning("Caught exception: " + e.Message);
				}
			}

			Log.Information("All services have been stopped.");
		}
	}
}