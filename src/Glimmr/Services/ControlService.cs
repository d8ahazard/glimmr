#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.Util;
using Glimmr.Enums;
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
using Newtonsoft.Json.Serialization;
using Serilog;
using Serilog.Core;
using Serilog.Events;

#endregion

namespace Glimmr.Services;

public class ControlService : BackgroundService {
	public bool SendPreview { get; set; }
	public ColorService ColorService { get; set; } = null!;
	public HttpClient HttpSender { get; private set; } = null!;
	public MulticastService MulticastService { get; private set; } = null!;
	public ServiceDiscovery ServiceDiscovery { get; private set; } = null!;
	public StatData? Stats { get; set; }
	public UdpClient UdpClient { get; private set; } = null!;

	private static LoggingLevelSwitch? _levelSwitch;

	private static ControlService _myCs = null!;
	
	private readonly IHubContext<SocketServer> _hubContext;
	
	public AsyncEvent<DynamicEventArgs> DemoLedEvent = null!;
	public AsyncEvent<DynamicEventArgs> DeviceReloadEvent = null!;
	public AsyncEvent<DynamicEventArgs> DeviceRescanEvent = null!;
	public AsyncEvent<DynamicEventArgs> FlashDeviceEvent = null!;
	public AsyncEvent<DynamicEventArgs> FlashSectorEvent = null!;
	public AsyncEvent<DynamicEventArgs> RefreshLedEvent = null!;
	public AsyncEvent<DynamicEventArgs> SetModeEvent = null!;
	public AsyncEvent<DynamicEventArgs> StartStreamEvent = null!;
	public AsyncEvent<DynamicEventArgs> TestLedEvent = null!;

	private Dictionary<string, dynamic> _agents = null!;
	private readonly Task _colorTask = null!;
	private readonly Task _discoveryTask = null!;
	private SystemData _sd = null!;
	private readonly Task _statTask = null!;

	public ControlService(IHubContext<SocketServer> hubContext) {
		_myCs = this;
		_hubContext = hubContext;
		_levelSwitch = Program.LogSwitch;
		Initialize();
	}

	public static ControlService GetInstance() {
		return _myCs;
	}

	public event Action RefreshSystemEvent = delegate { };

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
			try {
				dynamic? agentCheck = Activator.CreateInstance(Type.GetType(c)!);
				if (agentCheck == null) {
					Log.Warning($"Agent maker for {c} is null!");
				} else {
					var agentMaker = (IColorTargetAgent)agentCheck;
					var agent = agentMaker.CreateAgent(this);
					if (agent != null) {
						_agents[shortClass] = agent;
					} else {
						Log.Information($"Agent {c} is null.");
					}
				}
			} catch (Exception e) {
				Log.Warning("Agent creation error: " + e.Message);
			}
		}
	}

	public async Task ScanDevices() {
		await DeviceRescanEvent.InvokeAsync(this, new DynamicEventArgs("foo"));
	}

	public async Task SetMode(DeviceMode mode, bool autoDisabled = false) {
		Log.Information("Setting mode: " + mode);
		if (mode != DeviceMode.Off) {
			DataUtil.SetItem("PreviousMode", mode);
		}

		DataUtil.SetItem("AutoDisabled", autoDisabled);
		ColorUtil.SetSystemData();
		await SetModeEvent.InvokeAsync(this, new DynamicEventArgs(mode));
	}

	public async Task<bool> AuthorizeDevice(string id, IClientProxy? clientProxy = null) {
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

				return true;
			}
		} else {
			Log.Warning("Device is null: " + id);
			if (clientProxy != null) {
				await clientProxy.SendAsync("auth", "error");
			}

			return false;
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

		if (dev == null) {
			return false;
		}

		if (dev.GetType() == null) {
			return false;
		}

		if (dev.GetType().GetMethod("CheckAuthAsync") != null) {
			var count = 0;
			while (count < 30) {
				try {
					var activated = await dev.CheckAuthAsync(data).ConfigureAwait(false);
					if (!string.IsNullOrEmpty(activated.Token)) {
						Log.Information("Device is activated!");
						await DataUtil.AddDeviceAsync(activated, true);
						if (clientProxy != null) {
							await clientProxy.SendAsync("auth", "authorized");
						}

						await _hubContext.Clients.All.SendAsync("device",
							JsonConvert.SerializeObject((IColorTargetData)activated));
						return true;
					}
				} catch (Exception e) {
					Log.Warning("Error: " + e.Message + " at " + e.StackTrace);
				}

				await Task.Delay(TimeSpan.FromSeconds(1));
				count++;
				if (clientProxy == null) {
					continue;
				}

				await clientProxy.SendAsync("auth", "update", count);
				return false;
			}
		} else {
			Log.Warning("Error creating activator!");
			if (clientProxy == null) {
				return false;
			}

			await clientProxy.SendAsync("auth", "error");
			return false;
		}

		return false;
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


	public async Task NotifyClients() {
		await _hubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized(this));
	}

	protected override Task ExecuteAsync(CancellationToken stoppingToken) {
		Log.Debug("Starting services...");
		Log.Debug("All services started, running main loop...");
		Task.Run(async () => {
			while (!stoppingToken.IsCancellationRequested) {
				await CheckBackups().ConfigureAwait(false);
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
				await Task.Delay(TimeSpan.FromMinutes(60), stoppingToken);
			}
		}, stoppingToken);
		return Task.CompletedTask;
	}

	private static Task CheckBackups() {
		var userPath = SystemUtil.GetUserDir();
		var dbFiles = Directory.GetFiles(userPath, "*.db");
		if (dbFiles.Length == 1) {
			Log.Debug("Backing up database...");
			DataUtil.BackupDb();
		}

		var userDir = SystemUtil.GetUserDir();
		var stamp = DateTime.Now.ToString("yyyyMMdd");
		var outFile = Path.Combine(userDir, $"store_{stamp}.db");
		if (!dbFiles.Contains(outFile)) {
			Log.Debug($"Backing up database for {stamp}...");
			DataUtil.BackupDb();
		}

		Array.Sort(dbFiles);
		Array.Reverse(dbFiles);
		if (dbFiles.Length <= 8) {
			return Task.CompletedTask;
		}

		foreach (var p in dbFiles) {
			var pStamp = p.Replace(userDir, "");
			pStamp = pStamp.Replace(Path.DirectorySeparatorChar.ToString(), "");
			pStamp = pStamp.Replace("store_", "");
			pStamp = pStamp.Replace(".db", "");
			var diff = DateTime.Now - DateTime.ParseExact(pStamp, "yyyyMMdd", CultureInfo.InvariantCulture);
			if (diff <= TimeSpan.FromDays(7)) {
				continue;
			}

			if (p.Equals(Path.Combine(userDir, "store.db"))) {
				continue;
			}

			if (p.Contains("-log")) {
				continue;
			}

			try {
				Log.Debug("Deleting old db backup: " + p);
				File.Delete(p);
				Log.Debug("Deleted...");
			} catch (Exception e) {
				Log.Warning($"Exception removing file({p}): " + e.Message);
			}
		}

		return Task.CompletedTask;
	}

	private void Initialize() {
		var entry = Assembly.GetEntryAssembly();
		var version = "1.1.0";
		if (entry != null) {
			try {
				version = entry.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
			} catch (Exception) {
				// ignore
			}
		}


		var text = $@"

 (                                            (    (      *      *    (     
 )\ )   )            )                 (      )\ ) )\ ) (  `   (  `   )\ )  
(()/(( /(   ) (   ( /((        (  (    )\ )  (()/((()/( )\))(  )\))( (()/(  
 /(_))\()| /( )(  )\())\  (    )\))(  (()/(   /(_))/(_)|(_)()\((_)()\ /(_)) 
(_))(_))/)(_)|()\(_))((_) )\ )((_))\   /(_))_(_)) (_)) (_()((_|_()((_|_))   
/ __| |_((_)_ ((_) |_ (_)_(_/( (()(_) (_)) __| |  |_ _||  \/  |  \/  | _ \  
\__ \  _/ _` | '_|  _|| | ' \)) _` |    | (_ | |__ | | | |\/| | |\/| |   /  
|___/\__\__,_|_|  \__||_|_||_|\__, |     \___|____|___||_|  |_|_|  |_|_|_\  
                              |___/                                         
v. {version}
";
		Log.Information(text);
		Log.Information("Initializing control service...");
		_sd = DataUtil.GetSystemData();
		SetLogLevel();
		var devs = DataUtil.GetDevices();
		if (devs.Count == 0) {
			if (SystemUtil.IsRaspberryPi()) {
				var ld0 = new LedData { Id = "0", Brightness = 255, GpioNumber = 18, Enable = true };
				var ld1 = new LedData { Id = "1", Brightness = 255, GpioNumber = 19 };
				DataUtil.AddDeviceAsync(ld0).ConfigureAwait(false);
				DataUtil.AddDeviceAsync(ld1).ConfigureAwait(false);
			}
		}

		_agents = new Dictionary<string, dynamic>();
		// Now we can load stuff
		// Init nano HttpClient
		HttpSender = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
		// Init UDP client
		UdpClient = new UdpClient { Ttl = 5 };
		UdpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
		// This should keep our socket from doing bad things?
		UdpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 30);
		UdpClient.Client.Blocking = false;
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
			UdpClient.DontFragment = true;
		}

		MulticastService = new MulticastService();
		ServiceDiscovery = new ServiceDiscovery(MulticastService);
		LoadAgents();
		ColorUtil.SetSystemData();
		Log.Information("Control service initialized...");
	}

	private void SetLogLevel() {
		if (_levelSwitch == null) {
			return;
		}

		_levelSwitch.MinimumLevel = _sd.LogLevel switch {
			0 => LogEventLevel.Verbose,
			1 => LogEventLevel.Debug,
			2 => LogEventLevel.Information,
			4 => LogEventLevel.Warning,
			5 => LogEventLevel.Error,
			6 => LogEventLevel.Fatal,
			_ => _levelSwitch.MinimumLevel
		};
	}

	public override async Task StopAsync(CancellationToken cancellationToken) {
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

		await Task.WhenAll(_statTask, _colorTask, _discoveryTask);
		Log.Information("Control service stopped.");
	}


	public async Task AddDevice(IColorTargetData data) {
		await DataUtil.AddDeviceAsync(data);
		Log.Debug("Adding device " + data.Name);
		IColorTargetData? data2 = DataUtil.GetDevice(data.Id) ?? null;
		if (data2 != null) {
			var serializerSettings = new JsonSerializerSettings {
				ContractResolver = new CamelCasePropertyNamesContractResolver()
			};

			await _hubContext.Clients.All.SendAsync("device", JsonConvert.SerializeObject(data2, serializerSettings));
		}
	}

	public async Task SendLogLine(LogEvent message) {
		await _hubContext.Clients.All.SendAsync("log", JsonConvert.SerializeObject(message));
	}


	public async Task UpdateSystem(SystemData? sd = null) {
		var oldSd = DataUtil.GetSystemData();
		var ll = oldSd.LogLevel;
		if (sd != null) {
			DataUtil.SetSystemData(sd);
			_sd = sd;
			if (oldSd.LedCount != sd.LedCount) {
				var leds = DataUtil.GetDevices<LedData>("Led");

				foreach (var colorTargetData in leds.Where(led => led.LedCount == 0)) {
					colorTargetData.LedCount = sd.LedCount;
					await DataUtil.AddDeviceAsync(colorTargetData);
				}
			}

			if (oldSd.OpenRgbIp != sd.OpenRgbIp || oldSd.OpenRgbPort != sd.OpenRgbPort) {
				LoadAgents();
			}

			if (ll != _sd.LogLevel) {
				SetLogLevel();
			}
		}

		ColorUtil.SetSystemData();
		await _hubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized(this));
		RefreshSystemEvent.Invoke();
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
		await DataUtil.AddDeviceAsync(device, merge).ConfigureAwait(false);
		await RefreshDevice(device.Id);
	}

	public async Task SendImage(string method, Mat image) {
		var vb = new VectorOfByte();
		CvInvoke.Imencode(".png", image, vb);
		await _hubContext.Clients.All.SendAsync(method, vb.ToArray());
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

	public async Task<bool> RemoveDevice(string id) {
		ColorService.StopDevice(id, true);
		var res = DataUtil.DeleteDevice(id);
		if (res) {
			await _hubContext.Clients.All.SendAsync("deleteDevice", id);
		}

		return res;
	}

	public async Task StartStream(GlimmrData gd) {
		await StartStreamEvent.InvokeAsync(this, new DynamicEventArgs(gd));
	}
}