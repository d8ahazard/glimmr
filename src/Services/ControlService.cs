#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Hubs;
using Glimmr.Models;
using Glimmr.Models.ColorTarget;
using Glimmr.Models.ColorTarget.Glimmr;
using Glimmr.Models.ColorTarget.Led;
using Glimmr.Models.Util;
using Makaretu.Dns;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Services {
	public class ControlService : BackgroundService {
		public ColorService ColorService { get; set; }
		public HttpClient HttpSender { get; set; }

		public MulticastService MulticastService { get; set; }
		public ServiceDiscovery ServiceDiscovery { get; set; }
		public UdpClient UdpClient { get; set; }
		private readonly IHubContext<SocketServer> _hubContext;

		public AsyncEvent<DynamicEventArgs> DemoLedEvent;

		public AsyncEvent<DynamicEventArgs> DeviceReloadEvent;
		public AsyncEvent<DynamicEventArgs> DeviceRescanEvent;

		public AsyncEvent<DynamicEventArgs> FlashDeviceEvent;

		public AsyncEvent<DynamicEventArgs> FlashSectorEvent;
		public AsyncEvent<DynamicEventArgs> RefreshLedEvent;

		public AsyncEvent<DynamicEventArgs> SetModeEvent;

		public AsyncEvent<DynamicEventArgs> StartStreamEvent;

		public AsyncEvent<DynamicEventArgs> TestLedEvent;

		private Dictionary<string, dynamic> _agents;
		private SystemData _sd;

#pragma warning disable 8618
		public ControlService(IHubContext<SocketServer> hubContext) {
#pragma warning restore 8618
			_hubContext = hubContext;
			Initialize();

		}

		public event Action RefreshSystemEvent = delegate { };

		public event Action<List<Color>, List<Color>, int, bool> TriggerSendColorEvent = delegate { };

		public dynamic? GetAgent(string classType) {
			foreach (var (key, value) in _agents) {
				if (key == classType) {
					return value;
				}
			}

			Log.Warning($"Error finding agent of type {classType}.");
			return null;
		}

		private void LoadAgents() {
			foreach (var a in _agents.Values) {
				try {
					a.Dispose();
				} catch (Exception) {
					//ignored
				}
			}

			_agents = new Dictionary<string, dynamic>();
			var types = SystemUtil.GetClasses<IColorTargetAgent>();
			foreach (var c in types) {
				var parts = c.Split(".");
				var shortClass = parts[^1];
				Log.Information("Creating agent: " + c);
				try {
					dynamic? agentCheck = Activator.CreateInstance(Type.GetType(c)!);
					if (agentCheck == null) {
						Log.Warning($"Agent maker for {c} is null!");
					} else {
						var agentMaker = (IColorTargetAgent) agentCheck;
						var agent = agentMaker.CreateAgent(this);
						if (agent != null) {
							_agents[shortClass] = agent;
						} else {
							Log.Information($"Agent {c} is null.");
						}
					}
				} catch (Exception e) {
					Log.Debug("Agent creation error: " + e.Message);
				}
			}
		}

		public async Task EnableDevice(string devId) {
			var dev = DataUtil.GetDevice(devId);
			if (dev == null) {
				return;
			}

			dev.Enable = true;
			await DataUtil.AddDeviceAsync(dev);
			await DeviceReloadEvent.InvokeAsync(this, new DynamicEventArgs(devId));
		}

		public async Task ScanDevices() {
			await DeviceRescanEvent.InvokeAsync(this, new DynamicEventArgs("foo"));
		}

		public async Task SetMode(int mode) {
			Log.Information("Setting mode: " + mode);
			if (mode != 0) {
				DataUtil.SetItem("PreviousMode", mode);
			}

			await _hubContext.Clients.All.SendAsync("mode", mode);
			DataUtil.SetItem("DeviceMode", mode);
			ColorUtil.SetSystemData();
			await SetModeEvent.InvokeAsync(this, new DynamicEventArgs(mode));
		}

		public async Task AuthorizeDevice(string id, IClientProxy? clientProxy = null) {
			var data = DataUtil.GetDevice(id);
			if (data != null) {
				if (string.IsNullOrEmpty(data.Token)) {
					Log.Information("Starting auth check...");
					if (clientProxy != null) {
						await clientProxy.SendAsync("auth", "start");
					}
				} else {
					Log.Information("Device is already authorized...");
					if (clientProxy != null) {
						await clientProxy.SendAsync("auth", "authorized");
					}

					return;
				}
			} else {
				Log.Warning("Device is null: " + id);
				if (clientProxy != null) {
					await clientProxy.SendAsync("auth", "error");
				}

				return;
			}


			var disco = SystemUtil.GetClasses<IColorTargetAuth>();
			dynamic? dev = null;
			foreach (var d in disco) {
				var baseStr = d.ToLower().Split(".")[^2];
				if (baseStr == data.Tag.ToLower()) {
					dev = Activator.CreateInstance(Type.GetType(d)!, ColorService);
				}

				if (dev != null) {
					break;
				}
			}

			if (dev != null && dev.GetType().GetMethod("CheckAuthAsync") != null) {
				var count = 0;
				while (count < 30) {
					try {
						var activated = await dev.CheckAuthAsync(data);
						if (!string.IsNullOrEmpty(activated.Token)) {
							Log.Information("Device is activated!");
							await DataUtil.AddDeviceAsync(activated, true);
							if (clientProxy != null) {
								await clientProxy.SendAsync("auth", "authorized");
							}

							await _hubContext.Clients.All.SendAsync("device",
								JsonConvert.SerializeObject((IColorTargetData) activated));
							return;
						}
					} catch (Exception e) {
						Log.Warning("Error: " + e.Message + " at " + e.StackTrace);
					}

					await Task.Delay(1000);
					count++;
					if (clientProxy != null) {
						await clientProxy.SendAsync("auth", "update", count);
					}
				}
			} else {
				Log.Warning("Error creating activator!");
				if (clientProxy != null) {
					await clientProxy.SendAsync("auth", "error");
				}
			}
		}


		public void TriggerImageUpdate() {
			_hubContext.Clients.All.SendAsync("loadPreview");
		}

		public async Task TestLights(int led) {
			await TestLedEvent.InvokeAsync(this, new DynamicEventArgs(led));
		}


		/// <summary>
		///     Call this to trigger device refresh
		/// </summary>
		private async Task RefreshDevice(string id) {
			await DeviceReloadEvent.InvokeAsync(this, new DynamicEventArgs(id));
		}


		// We call this one to send colors to everything, including the color service
		public void SendColors(List<Color> c1, List<Color> c2, int fadeTime = 0, bool force = false) {
			TriggerSendColorEvent(c1, c2, fadeTime, force);
		}


		public async Task NotifyClients() {
			await _hubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
		}

		protected override Task ExecuteAsync(CancellationToken stoppingToken) {
			return Task.Run(async () => {
				while (!stoppingToken.IsCancellationRequested) {
					await Task.Delay(60000, stoppingToken);
					if (!_sd.AutoUpdate) {
						continue;
					}

					var start = new TimeSpan(_sd.AutoUpdateTime, 0, 0); //10 o'clock
					var end = new TimeSpan(_sd.AutoUpdateTime, 1, 0); //12 o'clock
					var now = DateTime.Now.TimeOfDay;

					if (now <= start || now >= end) {
						continue;
					}

					Log.Information("Triggering system update.");
					SystemUtil.Update();
				}

				Log.Debug("Control service stopped.");
				return Task.CompletedTask;
			}, stoppingToken);
		}

		private void Initialize() {
			const string text = @"

 (                                            (    (      *      *    (     
 )\ )   )            )                 (      )\ ) )\ ) (  `   (  `   )\ )  
(()/(( /(   ) (   ( /((        (  (    )\ )  (()/((()/( )\))(  )\))( (()/(  
 /(_))\()| /( )(  )\())\  (    )\))(  (()/(   /(_))/(_)|(_)()\((_)()\ /(_)) 
(_))(_))/)(_)|()\(_))((_) )\ )((_))\   /(_))_(_)) (_)) (_()((_|_()((_|_))   
/ __| |_((_)_ ((_) |_ (_)_(_/( (()(_) (_)) __| |  |_ _||  \/  |  \/  | _ \  
\__ \  _/ _` | '_|  _|| | ' \)) _` |    | (_ | |__ | | | |\/| | |\/| |   /  
|___/\__\__,_|_|  \__||_|_||_|\__, |     \___|____|___||_|  |_|_|  |_|_|_\  
                              |___/                                         

";
			Log.Information(text);
			Log.Information("Starting control service...");
			_sd = DataUtil.GetSystemData();
			var devs = DataUtil.GetDevices();
			if (devs.Count == 0) {
				if (SystemUtil.IsRaspberryPi()) {
					var ld0 = new LedData {Id = "0", Brightness = 255, GpioNumber = 18, Enable = true};
					var ld1 = new LedData {Id = "1", Brightness = 255, GpioNumber = 19};
					DataUtil.AddDeviceAsync(ld0).ConfigureAwait(false);
					DataUtil.AddDeviceAsync(ld1).ConfigureAwait(false);
				}
			}

			_agents = new Dictionary<string, dynamic>();
			// Now we can load stuff
			// Init nano HttpClient
			HttpSender = new HttpClient {Timeout = TimeSpan.FromSeconds(5)};
			// Init UDP client
			UdpClient = new UdpClient {Ttl = 5};
			UdpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			UdpClient.Client.Blocking = false;
			UdpClient.DontFragment = true;
			MulticastService = new MulticastService();
			ServiceDiscovery = new ServiceDiscovery(MulticastService);
			LoadAgents();
			// Dynamically load agents
			ColorUtil.SetSystemData();
			Log.Information("Control service started.");
		}

		public override Task StopAsync(CancellationToken cancellationToken) {
			Log.Information("Stopping control service...");
			HttpSender.Dispose();
			UdpClient.Dispose();
			MulticastService.Dispose();
			foreach (dynamic agent in _agents) {
				try {
					var type = agent.GetType();
					if (type.GetMethod("Dispose") != null) {
						agent.Dispose();
					}
				} catch {
					// Ignored
				}
			}

			Log.Information("Control service stopped.");
			return base.StopAsync(cancellationToken);
		}


		public async Task AddDevice(IColorTargetData data) {
			await DataUtil.AddDeviceAsync(data);
		}


		public async Task UpdateSystem(SystemData? sd = null) {
			var oldSd = DataUtil.GetSystemData();
			if (sd != null) {
				DataUtil.SetSystemData(sd);
				_sd = sd;
				if (oldSd.LedCount != sd.LedCount) {
					var leds = DataUtil.GetDevices<LedData>("Led");

					foreach (var colorTargetData in leds.Where(led => led.LedCount == 0)) {
						var led = colorTargetData;
						led.LedCount = sd.LedCount;
						await DataUtil.AddDeviceAsync(led);
					}
				}

				if (oldSd.OpenRgbIp != sd.OpenRgbIp || oldSd.OpenRgbPort != sd.OpenRgbPort) {
					LoadAgents();
				}
			}

			ColorUtil.SetSystemData();
			RefreshSystemEvent();
		}

		public static Task SystemControl(string action) {
			switch (action) {
				case "restart":
					SystemUtil.Restart();
					break;
				case "shutdown":
					SystemUtil.Shutdown();
					break;
				case "reboot":
					SystemUtil.Reboot();
					break;
				case "update":
					SystemUtil.Update();
					break;
			}

			return Task.CompletedTask;
		}

		public async Task UpdateDevice(dynamic device, bool merge = true) {
			Log.Debug($"Updating {device.Tag}...");
			await DataUtil.AddDeviceAsync(device, merge);
			await _hubContext.Clients.All.SendAsync("device", JsonConvert.SerializeObject((IColorTargetData) device));
			await RefreshDevice(device.Id);
		}

		public async Task FlashDevice(string deviceId) {
			await FlashDeviceEvent.InvokeAsync(this, new DynamicEventArgs(deviceId));
		}

		public async Task FlashSector(int sector) {
			await FlashSectorEvent.InvokeAsync(this, new DynamicEventArgs(sector));
		}

		public async Task DemoLed(string id) {
			await DemoLedEvent.InvokeAsync(this, new DynamicEventArgs(id));
		}

		public async Task RemoveDevice(string id) {
			ColorService.StopDevice(id, true);
			DataUtil.RemoveDevice(id);
			await _hubContext.Clients.All.SendAsync("deleteDevice", id);
		}

		public async Task StartStream(GlimmrData gd) {
			await StartStreamEvent.InvokeAsync(this, new DynamicEventArgs(gd));
		}
	}
}