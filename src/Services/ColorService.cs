#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models;
using Glimmr.Models.ColorSource.Ambient;
using Glimmr.Models.ColorSource.Audio;
using Glimmr.Models.ColorSource.AudioVideo;
using Glimmr.Models.ColorSource.Video;
using Glimmr.Models.ColorTarget;
using Glimmr.Models.Util;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Serilog;
using Color = System.Drawing.Color;

// ReSharper disable All

#endregion

namespace Glimmr.Services {
	// Handles capturing and sending color data
	public class ColorService : BackgroundService {
		public ControlService ControlService { get; }
		private AmbientStream _ambientStream;
		private AudioStream _audioStream;
		private VideoStream _videoStream;
		private AudioVideoStream _avStream;

		private bool _autoDisabled;
		private int _captureMode;
		private int _deviceGroup;
		private int _deviceMode;
		//private float _fps;
		private SystemData _systemData;

		// Figure out how to make these generic, non-callable
		
		private IColorTarget[] _sDevices;
		
		// Token for the color target
		private CancellationTokenSource _sendTokenSource;
		
		// Token for the color source
		private CancellationTokenSource _streamTokenSource;
		// Generates a token every time we send colors, and expires super-fast
		private CancellationToken _stopToken;
		private bool _streamStarted;
		private Stopwatch _watch;
		
		public event Action<List<Color>, List<Color>, int, bool> ColorSendEvent = delegate {};
		
		public ColorService(ControlService controlService) {
			ControlService = controlService;
			ControlService.TriggerSendColorEvent += SendColors;
			ControlService.SetModeEvent += Mode;
			ControlService.DeviceReloadEvent += RefreshDeviceData;
			ControlService.RefreshLedEvent += ReloadLedData;
			ControlService.RefreshSystemEvent += ReloadSystemData;
			ControlService.TestLedEvent += LedTest;
			ControlService.FlashDeviceEvent += FlashDevice;
			ControlService.FlashSectorEvent += FlashSector;
			ControlService.DemoLedEvent += Demo;
			Log.Debug("Initialization complete.");
		}
		
		protected override Task ExecuteAsync(CancellationToken stoppingToken) {
			_stopToken = stoppingToken;
			_watch = new Stopwatch();
			_streamTokenSource = new CancellationTokenSource();
			Log.Information("Starting colorService loop...");
			LoadData();
			
			Log.Information("All color services have been initialized.");
			return Task.Run(async () => {
				Log.Debug("Running demo...");
				await Demo(this, new DynamicEventArgs());
				Log.Information($"All color sources initialized, setting mode to {_deviceMode}.");
				await Mode(this, new DynamicEventArgs(_deviceMode)).ConfigureAwait(true);

				while (!stoppingToken.IsCancellationRequested) {
					await CheckAutoDisable();
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

		public void AddStream(string name, BackgroundService stream) {
			switch (name) {
				case "audio":
					_audioStream = (AudioStream) stream;
					break;
				case "video":
					_videoStream = (VideoStream) stream;
					break;
				case "av":
					_avStream = (AudioVideoStream) stream;
					break;
				case "ambient":
					_ambientStream = (AmbientStream) stream;
					break;
			}
		}

		public BackgroundService GetStream(string name) {
			switch (name) {
				case "audio":
					return _audioStream;
				case "video":
					return _videoStream;
			}

			return null;
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
			var sectorCount = ((_systemData.HSectors + _systemData.VSectors) * 2) - 4;
			var sectors = ColorUtil.EmptyColors(new Color[sectorCount]);
			if (sector < sectorCount) sectors[sector] = col;
			var black = ColorUtil.EmptyList(_systemData.LedCount);
			var blackSectors = ColorUtil.EmptyList(sectorCount);
			var devices = new List<IColorTarget>();
			for (var i = 0; i < _sDevices.Length; i++) {
				if (_sDevices[i].Enable) {
					devices.Add((IColorTarget)_sDevices[i]);
				}
			}
			foreach (var _strip in devices) {
				if (_strip != null) {
					_strip.Testing = true;
				}
			}
			
			foreach (var _strip in devices) {
				if (_strip != null) {
					await _strip.SetColor(colors.ToList(), sectors.ToList(), 0);
				}
			}

			Thread.Sleep(500);
			
			foreach (var _strip in devices) {
				if (_strip != null) {
					_strip.SetColor(black,blackSectors, 0);
				}
			}
			
			foreach (var _strip in devices) {
				if (_strip != null) {
					_strip.SetColor(colors.ToList(), sectors.ToList(), 0);
				}
			}
			
			Thread.Sleep(1000);
			
			foreach (var _strip in devices) {
				if (_strip != null) {
					_strip.SetColor(black, blackSectors, 0);
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
				ControlService.SetModeEvent -= Mode;
				await ControlService.SetMode(_deviceMode);
				ControlService.SetModeEvent += Mode;
			} else {
				if (_autoDisabled || _deviceMode == 2) return;
				Log.Debug("Auto-disabling stream.");
				_autoDisabled = true;
				DataUtil.SetItem("AutoDisabled", _autoDisabled);
				ControlService.SetModeEvent -= Mode;
				await ControlService.SetMode(0);
				ControlService.SetModeEvent += Mode;
			}
		}

		
		private async Task LedTest(object o, DynamicEventArgs dynamicEventArgs) {
			int led = dynamicEventArgs.P1;
			var strips = new List<IColorTarget>();
			for (var i = 0; i < _sDevices.Length; i++) {
				if (_sDevices[i].Enable == true) {
					strips.Add((IColorTarget)_sDevices[i]);
				}
			}

			var colors = ColorUtil.EmptyList(_systemData.LedCount);
			var blackColors = colors;
			colors[led] = Color.FromArgb(255, 0, 0);
			var sectors = ColorUtil.LedsToSectors(colors, _systemData);
			var blackSectors = ColorUtil.EmptyList(_systemData.SectorCount);
			foreach (var dev in _sDevices) {
				dev.Testing = true;
				dev.SetColor(colors, sectors,0,true);
			}
			await Task.Delay(500);
			foreach (var dev in _sDevices) {
				dev.SetColor(blackColors, blackSectors,0,true);
			}
			await Task.Delay(500);
			foreach (var dev in _sDevices) {
				dev.SetColor(colors, sectors,0,true);
			}
			await Task.Delay(500);
			foreach (var dev in _sDevices) {
				dev.SetColor(blackColors, blackSectors,0,true);
				dev.Testing = false;
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
			Log.Debug("Loading strip");
			_systemData = DataUtil.GetObject<SystemData>("SystemData");
			Log.Debug("Creating new device lists...");
			// Create new lists
			var sDevs = new List<IColorTarget>();
			var classes = SystemUtil.GetClasses<IColorTarget>();
			var deviceData = DataUtil.GetCollection<dynamic>("Devices");
			var enabled = 0;
			foreach (var c in classes) {
				try {
					Log.Debug("Loading class: " + c);
					var tag = c.Replace("Device", "");
					var dataName = c.Replace("Device", "Data");
					tag = c.Replace("Glimmr.Models.ColorTarget.", "");
					tag = tag.Split(".")[0];
					Log.Debug("Tag: " + tag);
					foreach (var device in deviceData) {
						if (device.Tag == tag && device.Tag != "Corsair") {
							Log.Debug("Loading dev data: " + JsonConvert.SerializeObject(device));
							if (device.Enable) enabled++;
							var args = new object[] {device, this};
							var obj = (IColorTarget) Activator.CreateInstance(Type.GetType(c)!, args);
							sDevs.Add(obj);
						}
					}
					
				} catch (Exception e) {
					Log.Warning("Exception: " + e.Message);
				}
			}

			Log.Debug("We have a total of " + sDevs.Count + $" devices, {enabled} are enabled.");
			_sDevices = sDevs.ToArray();
		}

		private async Task Demo(object o, DynamicEventArgs dynamicEventArgs) {
			StartStream();
			Log.Debug("Demo fired...");
			var ledCount = 300;
			var sectorCount = _systemData.SectorCount;
			try {
				ledCount = _systemData.LedCount;
			} catch (Exception) {
				
			}
			Log.Debug("Running demo on " + ledCount + $"leds, {sectorCount} sectors.");
			var i = 0;
			var cols = new List<Color>();
			var secs = new List<Color>();
			cols = ColorUtil.EmptyList(ledCount);
			secs = ColorUtil.EmptyList(sectorCount);
			int degs = ledCount / sectorCount;
			while (i < ledCount) {
				var pi = i * 1.0f;
				var progress = pi / ledCount;
				var rCol = ColorUtil.Rainbow(progress);
				secs = ColorUtil.LedsToSectors(cols, _systemData);
				cols[i] = rCol;
				
				try {
					SendColors(cols, secs, 0, true);
				} catch (Exception e) {
					Log.Warning("SEND EXCEPTION: " + JsonConvert.SerializeObject(e));
				}
				

				i++;
			}

			// Finally show off our hard work
			await Task.Delay(500);
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
			foreach (var dev in _sDevices) {
				if (dev.Id == id) {
					await dev.StopStream();
					await dev.ReloadData();
					if (!dev.IsEnabled()) {
						return;
					}

					exists = true;
					await dev.StartStream(_sendTokenSource.Token);
				}
			}
			
			
			if (exists) {
				return;
			}

			
			var sda = DataUtil.GetDevice(id);
			
			// If our device is a real boy, start it and add it
			if (sda == null) {
				return;
			}

			var newDev = CreateDevice(sda);
			await sda.StartStream(_sendTokenSource.Token);

			var sDevs = _sDevices.ToList();
			sDevs.Add(sda);
			_sDevices = sDevs.ToArray();
		}

		private dynamic CreateDevice(dynamic devData) {
			var classes = SystemUtil.GetClasses<IColorTarget>();
			var deviceData = DataUtil.GetCollection<dynamic>("Devices");
			foreach (var c in classes) {
				try {
					Log.Debug("Loading class: " + c);
					var tag = c.Replace("Device", "");
					var dataName = c.Replace("Device", "Data");
					tag = c.Replace("Glimmr.Models.ColorTarget.", "");
					tag = tag.Split(".")[0];
					Log.Debug("Tag: " + tag);
					if (devData.Tag == tag) {
						var args = new object[] {devData, this};
						var obj = (IColorTarget) Activator.CreateInstance(Type.GetType(c)!, args);
						return obj;
					}
					
					
				} catch (Exception e) {
					Log.Warning("Exception: " + e.Message);
				}
			}

			return null;
		}

		private Task ReloadLedData(object o, DynamicEventArgs dynamicEventArgs) {
			string ledId = dynamicEventArgs.P1;
			foreach (var dev in _sDevices) {
				if (dev.Id == ledId) {
					dev.ReloadData();
				}
			}
			return Task.CompletedTask;
		}

		
		private void ReloadSystemData() {
			_videoStream?.Refresh();
			_audioStream?.Refresh();
			var sd = _systemData;
			_systemData = DataUtil.GetObject<SystemData>("SystemData");

			var refreshAmbient = false;
			if (sd.AmbientColor != _systemData.AmbientColor) {
				refreshAmbient = true;
			}

			if (sd.AmbientMode != _systemData.AmbientMode) {
				refreshAmbient = true;
			}

			if (sd.AmbientShow != _systemData.AmbientShow) {
				refreshAmbient = true;
			}

			if (refreshAmbient) {
				_ambientStream?.Refresh();
			}
			
		}

		private async Task Mode(object o, DynamicEventArgs dynamicEventArgs) {
			int newMode = dynamicEventArgs.P1;
			_deviceMode = newMode;
			if (newMode != 0 && _autoDisabled) {
				_autoDisabled = false;
				DataUtil.SetItem("AutoDisabled", _autoDisabled);
			}
			
			if (_streamStarted && newMode == 0) await StopStream();
			
			switch (newMode) {
				case 1:
					_audioStream.ToggleStream();
					_ambientStream.ToggleStream();
					_avStream.ToggleStream();
					_videoStream.ToggleStream(true);
					break;
				case 2: // Audio
					_ambientStream.ToggleStream();
					_avStream.ToggleStream();
					_videoStream.ToggleStream();
					_audioStream.ToggleStream(true);
					break;
				case 3: // Ambient
					_audioStream.ToggleStream();
					_avStream.ToggleStream();
					_videoStream.ToggleStream();
					_ambientStream.ToggleStream(true);
					break;
				case 4: // A/V mode :D
					_ambientStream.ToggleStream(false);
					_audioStream.ToggleStream(true);
					_audioStream.SendColors = false;
					_videoStream.ToggleStream(true);
					_videoStream.SendColors = false;
					_avStream.ToggleStream(true);
					break;
				case 5:
					_audioStream.ToggleStream();
					_avStream.ToggleStream();
					_videoStream.ToggleStream();
					_ambientStream.ToggleStream();
					// Nothing to do, this tells the app to get data from DreamService
					break;
			}
			
			if (newMode != 0 && !_streamStarted) StartStream();
			_deviceMode = newMode;
			Log.Information($"Device mode updated to {newMode}.");
		}



		private void StartStream() {
			if (!_streamStarted) {
				_streamStarted = true;
				Log.Information("Starting streaming devices...");
				foreach (var sdev in _sDevices) {
					sdev.StartStream(_sendTokenSource.Token);
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
			var streamers = new List<IColorTarget>();
			foreach (var s in _sDevices.Where(s => s.Streaming)) streamers.Add(s);
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


		public void SendColors(List<Color> colors, List<Color> sectors, int fadeTime, bool force = false) {
			_sendTokenSource ??= new CancellationTokenSource();
			if (_sendTokenSource.IsCancellationRequested) {
				Log.Debug("Send token is canceled.");
				return;
			}

			if (!_streamStarted) {
				Log.Debug("Stream not started...");
				return;
			}

			ColorSendEvent(colors, sectors, fadeTime, force);
		}


		private static void CancelSource(CancellationTokenSource target, bool dispose = false) {
			if (target == null) return;

			if (!target.IsCancellationRequested) target.CancelAfter(0);

			if (dispose) target.Dispose();
		}

		private async Task StopServices() {
			Log.Information("Stopping services...");
			_streamTokenSource?.Cancel();
			_avStream.ToggleStream();
			_audioStream.ToggleStream();
			_videoStream.ToggleStream();
			_ambientStream.ToggleStream();
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