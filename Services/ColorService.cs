using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Hubs;
using Glimmr.Models.CaptureSource.Audio;
using Glimmr.Models.CaptureSource.Video;
using Glimmr.Models.DreamScreen;
using Glimmr.Models.LED;
using Glimmr.Models.StreamingDevice;
using Glimmr.Models.StreamingDevice.Hue;
using Glimmr.Models.StreamingDevice.LIFX;
using Glimmr.Models.StreamingDevice.Nanoleaf;
using Glimmr.Models.StreamingDevice.WLed;
using Glimmr.Models.Util;
using LifxNet;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Color = System.Drawing.Color;

namespace Glimmr.Services {
	// Handles capturing and sending color data
	public class ColorService : BackgroundService {
		private IHubContext<SocketServer> _hubContext;
		private readonly ControlService _controlService;
		private LedStrip _strip;
		private StreamCapture _grabber;
		private AudioStream _aStream;
		private bool _autoDisabled;
		private bool _streamStarted;
		private bool _streamAudio;
		private CancellationTokenSource _captureTokenSource;
		private CancellationTokenSource _sendTokenSource;
		
		private LifxClient _lifxClient;
		private HttpClient _nanoClient;
		private Socket _nanoSocket;

		private List<WLedStrip> _wledStrips;
		private List<IStreamingDevice> _sDevices;

		private int _captureMode;
		private int _deviceMode;
		private int _deviceGroup;
		private int _ambientMode;
		private int _ambientShow;
		private string _ambientColor;
		private Dictionary<string, int> _subscribers;

		private Task _videoCaptureTask;


		public ColorService(IHubContext<SocketServer> hubContext, ControlService controlService) {
			_hubContext = hubContext;
			_controlService = controlService;
			_controlService.TriggerSendColorsEvent += SendColors;
			_controlService.TriggerSendColorsEvent2 += SendColors;
			_controlService.SetModeEvent += Mode;
			_controlService.DeviceReloadEvent += RefreshEventData;
			_wledStrips = new List<WLedStrip>();
			_sDevices = new List<IStreamingDevice>();
			LogUtil.Write("Initialization complete.");
		}
		
		protected override Task ExecuteAsync(CancellationToken stoppingToken) {
			LogUtil.Write("Starting colorService loop...");
			return Task.Run(async () => {
				// If our control service says so, refresh data on all devices while streaming
				LogUtil.Write("Control service starting...");
				_controlService.DeviceReloadEvent += RefreshEventData;
				_controlService.SetModeEvent += Mode;
				var watch = new Stopwatch();
				RefreshEventData();
				LogUtil.Write("Starting capture service...");
				StartCaptureServices(stoppingToken);
				LogUtil.Write("Starting capture task...");
				StartVideoCaptureTask();
				LogUtil.Write("Starting audio capture task...");
				StartAudioCaptureTask();
				LogUtil.Write("Color service tasks started, setting Mode...");
				// Start our initial mode
				Mode(_deviceMode);
				LogUtil.Write("All color service tasks started, really executing main loop...");
				while (!stoppingToken.IsCancellationRequested) {
					if (_grabber == null) continue;
					if (_grabber.SourceActive) {
						watch.Reset();
						if (!_autoDisabled) continue;
						_autoDisabled = false;
						LogUtil.Write("Auto-enabling stream.");
						_controlService.SetMode(_deviceMode);
					} else {
						if (_autoDisabled) continue;
						if (_deviceMode != 1) continue;
						if (!watch.IsRunning) watch.Start();
						if (watch.ElapsedMilliseconds > 5000) {
							LogUtil.Write("Auto-sleeping lights.");
							_autoDisabled = true;
							_deviceMode = 0;
							DataUtil.SetItem<bool>("AutoDisabled", _autoDisabled);
							DataUtil.SetItem<bool>("DeviceMode", _deviceMode);
							_controlService.SetMode(_deviceMode);
							watch.Reset();
						} else {
							if (watch.ElapsedMilliseconds % 1000 != 0) continue;
							watch.Reset();
						}
					}
					await Task.Delay(1, stoppingToken);
				}
				StopServices();
				LogUtil.Write("Color service stopped.");
				return Task.CompletedTask;
			});
			
		}

		private void RefreshEventData() {
			// Reload main vars
			_captureMode = DataUtil.GetItem<int>("CaptureMode") ?? 2;
			_deviceMode = DataUtil.GetItem<int>("DeviceMode") ?? 0;
			_deviceGroup = DataUtil.GetItem<int>("DeviceGroup") ?? 0;
			_ambientMode = DataUtil.GetItem<int>("AmbientMode") ?? 0;
			_ambientShow = DataUtil.GetItem<int>("AmbientShow") ?? 0;
			_ambientColor = DataUtil.GetItem<string>("AmbientColor") ?? "FFFFFF";
			
			// Dispose any devices we may have that are running
			foreach (var wl in _wledStrips) {
				wl.StopStream();
				wl.Dispose();
			}

			foreach (var s in _sDevices) {
				s.StopStream();
				s.Dispose();
			}
			
			// Create new lists
			_wledStrips = new List<WLedStrip>();
			_sDevices = new List<IStreamingDevice>();
			
			var bridgeArray = DataUtil.GetCollection<BridgeData>("Dev_Hue");
			_sendTokenSource = new CancellationTokenSource();
			foreach (var bridge in bridgeArray.Where(bridge => !string.IsNullOrEmpty(bridge.Key) && !string.IsNullOrEmpty(bridge.User) && bridge.SelectedGroup != "-1")) {
				_sDevices.Add(new HueBridge(bridge));
			}

			// Init leaves
			var leaves = DataUtil.GetCollection<NanoData>("Dev_NanoLeaf");
			foreach (var n in leaves.Where(n => !string.IsNullOrEmpty(n.Token) && n.Layout != null)) {
				_sDevices.Add(new NanoGroup(n, _nanoClient, _nanoSocket));
			}

			// Init lifx
			var lifx = DataUtil.GetCollection<LifxData>("Dev_Lifx");
			if (lifx != null) {
				foreach (var b in lifx.Where(b => b.TargetSector != -1)) {
					_sDevices.Add(new LifxBulb(b, _lifxClient));
				}
			}
                
			var wlArray = DataUtil.GetCollection<WLedData>("Dev_Wled");
			foreach (var wl in wlArray) {
				LogUtil.Write("Adding Wled device!");
				_wledStrips.Add(new WLedStrip(wl));
			}

			// If we are capturing, re-initialize splitter
			if (_videoCaptureTask != null) {
				CancelSource(_captureTokenSource);
				StartVideoCaptureTask();
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
					StopStream();
					break;
				case 1:
					if (!_streamStarted) StartStream();
					_grabber?.ToggleSend();
					_streamAudio = false;
					LogUtil.Write("Toggling send on grabber...");
					if (_captureMode == 0) _controlService.TriggerDreamSubscribe();
					break;
				case 2: // Audio
					if (!_streamStarted) StartStream();
					_grabber?.ToggleSend(false);
					_streamAudio = true;
					LogUtil.Write("Toggling send on grabber...");
					if (_captureMode == 0) _controlService.TriggerDreamSubscribe();
					break;
				case 3: // Ambient
					if (!_streamStarted) StartStream();
					_grabber?.ToggleSend(false);
					_streamAudio = false;
					LogUtil.Write("Toggling send on grabber...");
					
					break;
			}
		}
		
		// Starts our capture service and initializes our LEDs
		private void StartCaptureServices(CancellationToken ct) {
			if (_captureMode == 0) {
				LogUtil.Write("Capture mode is 0, we don't need to capture video.");
				return;
			}
			LedData ledData = DataUtil.GetObject<LedData>("LedData") ?? new LedData(true);
			try {
				_strip = new LedStrip(ledData);
				LogUtil.Write("Initialized LED strip...");
			} catch (TypeInitializationException e) {
				LogUtil.Write("Type init error: " + e.Message);
			}

			_grabber = new StreamCapture(ct);
			if (_grabber == null) return;
			Task.Run(() => SubscribeBroadcast(ct), ct);
		}
		
		private void StartVideoCaptureTask() {
			if (_captureMode == 0) {
				_controlService.TriggerDreamSubscribe();
			} else {
				CancelSource(_captureTokenSource);
				_captureTokenSource = new CancellationTokenSource();
				Task.Run(() => StartVideoCapture(_captureTokenSource.Token));
				LogUtil.Write("Video capture task has been started.");
			}
		}
		
		private Task StartVideoCapture(CancellationToken cancellationToken) {
			if (_grabber == null) {
				LogUtil.Write("Grabber is null!!", "WARN");
				return Task.CompletedTask;
			}
			LogUtil.Write("Starting grabber capture task...");
			_videoCaptureTask = _grabber.StartCapture(this, cancellationToken);
			LogUtil.Write("Created...");
			return _videoCaptureTask.IsCompleted ? Task.CompletedTask : _videoCaptureTask;
		}


		private Task StartAudioCapture(CancellationToken cancellation) {
			if (_aStream == null) {
				LogUtil.Write("No Audio devices, no stream.");
				return Task.CompletedTask;
			}
			if (cancellation != CancellationToken.None) {
				LogUtil.Write("Starting audio capture service.");
				_aStream.StartStream(cancellation);
				return Task.Run(() => {
					while (!cancellation.IsCancellationRequested) {
						if (!_streamAudio) continue;
						var cols = _aStream.GetColors();
						SendColors(cols, cols);
					}
				});
			}

			LogUtil.Write("Cancellation token is null??");
			return Task.CompletedTask;
		}

		private void StartAudioCaptureTask() {
			LogUtil.Write("Starting audio capture task.");
			Task.Run(() => StartAudioCapture(_captureTokenSource.Token));
		}
		
		private void StartStream() {
            if (!_streamStarted) {
                LogUtil.Write("Starting stream...");
                RefreshEventData();

                foreach (var sd in _sDevices.Where(sd => !sd.Streaming)) {
                    LogUtil.Write("Starting device: " + sd.IpAddress);
                    sd.StartStream(_sendTokenSource.Token);
                    LogUtil.Write($"Started device at {sd.IpAddress}.");
                }

                foreach (var wl in _wledStrips.Where(wl => !wl.Streaming)) {
                    wl.StartStream(_sendTokenSource.Token);
                    LogUtil.Write($"Started wled device at {wl.IpAddress}.");
                }
            }

            _streamStarted = true;
            if (_streamStarted) LogUtil.WriteInc("Streaming on all devices should now be started...");
        }
		
		private void StopStream() {
			if (!_streamStarted) return;
			_grabber?.ToggleSend(false);
			_streamAudio = false;
			CancelSource(_sendTokenSource);
			_strip?.StopLights();
                
			foreach (var s in _sDevices.Where(s => s.Streaming)) {
				s.StopStream();
			}
                
			foreach (var wl in _wledStrips.Where(wl => wl.Streaming)) {
				wl.StopStream();
			}
                
			LogUtil.WriteDec("Stream stopped.");
			_streamStarted = false;
		}


		private void SendColors(List<Color> colors, List<Color> sectors) {
			var fadeTime = 0;
			_sendTokenSource ??= new CancellationTokenSource();
			if (_sendTokenSource.IsCancellationRequested) return;
			if (!_streamStarted) return;
			var output = sectors;
			foreach (var sd in _sDevices) {
				sd.SetColor(output, fadeTime);
			}

			foreach (var wl in _wledStrips) {
				wl.SetColor(output, fadeTime, true);
			}

			if (_captureMode != 0) {
				// If we have subscribers and we're capturing
				if (_subscribers.Count > 0) {
					LogUtil.Write("We have " + _subscribers.Count + " subscribers: " + _captureMode);
				}

				if (_subscribers.Count > 0 && _captureMode != 0) {
					var keys = new List<string>(_subscribers.Keys);
					foreach (var ip in keys) {
						DreamSender.SendSectors(sectors, ip, _deviceGroup);
						LogUtil.Write("Sent.");
					}
				}
				LogUtil.Write("Updating strip...");
				_strip?.UpdateAll(colors);
			}
		}

		// If we pass in a third set of sectors, use that info instead.
		public void SendColors(List<Color> colors, List<Color> sectors, List<Color> sectorsV2, Dictionary<string, List<Color>> wledSectors) {
			_sendTokenSource ??= new CancellationTokenSource();
			if (wledSectors == null) throw new ArgumentNullException(nameof(wledSectors));
			if (_sendTokenSource.IsCancellationRequested) {
				LogUtil.Write("Canceled.");
				return;
			}

			if (!_streamStarted) {
				LogUtil.Write("Stream not started.");
				return;
			}

			var fadeTime = 0;
			foreach (var sd in _sDevices) {
				sd.SetColor(sectorsV2, fadeTime);
			}

			foreach (var wl in _wledStrips) {
				wl.SetColor(wledSectors[wl.Id], fadeTime);
			}

			if (_captureMode == 0) return;
			// If we have subscribers and we're capturing
			if (_subscribers.Count > 0 && _captureMode != 0) {
				var keys = new List<string>(_subscribers.Keys);
				foreach (var ip in keys) {
					DreamSender.SendSectors(sectors, ip, _deviceGroup);
				}
			}
			_strip?.UpdateAll(colors);
		}

		
		
		private async void SubscribeBroadcast(CancellationToken ct) {
			_subscribers = new Dictionary<string, int>();
			// Loop until canceled
			try {
				while (!ct.IsCancellationRequested) {
					// Send our subscribe multicast
					DreamSender.SendUdpWrite(0x01, 0x0C, new byte[] {0x01}, 0x30, (byte) _deviceGroup, null, true);
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

					// Sleep for 5s
					await Task.Delay(5000, ct);
				}
			} catch (TaskCanceledException) {
				_subscribers = new Dictionary<string, int>();
			}
		}
		
		private static void CancelSource(CancellationTokenSource target, bool dispose = false) {
			if (target == null) return;
			if (!target.IsCancellationRequested) {
				target.Cancel();
			}

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

			foreach (var w in _wledStrips) {
				w.StopStream();
				w.Dispose();
			}
			LogUtil.Write("All services have been stopped.");
		}

	}

}