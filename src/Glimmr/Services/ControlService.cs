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
using System.Timers;
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
using Timer = System.Timers.Timer;

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

	private static Timer? _ut;
	private static Timer? _bt;
	
	public AsyncEvent<DynamicEventArgs> DemoLedEvent = null!;
	public AsyncEvent<DynamicEventArgs> DeviceReloadEvent = null!;
	public AsyncEvent<DynamicEventArgs> DeviceRescanEvent = null!;
	public AsyncEvent<DynamicEventArgs> FlashDeviceEvent = null!;
	public AsyncEvent<DynamicEventArgs> FlashSectorEvent = null!;
	public AsyncEvent<DynamicEventArgs> RefreshLedEvent = null!;
	public AsyncEvent<DynamicEventArgs> SetModeEvent = null!;
	public AsyncEvent<DynamicEventArgs> StartStreamEvent = null!;
	public AsyncEvent<DynamicEventArgs> FlashLedEvent = null!;

	private Dictionary<string, dynamic> _agents = null!;
	private SystemData _sd;
	
	public ControlService(IHubContext<SocketServer> hubContext) {
		_myCs = this;
		ShowHeader();
		_sd = DataUtil.GetSystemData();
		_hubContext = hubContext;
		_levelSwitch = Program.LogSwitch;
		Initialize();
		_bt = new Timer();
		_bt.Interval = 1000 * 60 * 60;
		_bt.Elapsed += CheckBackups;
		_bt.Start();

		_ut = new Timer();
		_ut.Interval = 1000 * 60 * 60 * _sd.AutoUpdateTime;
		_ut.Elapsed += CheckUpdate;
		_ut.Start();
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
		DataUtil.SetItem("DeviceMode", mode);
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
			var serializerSettings = new JsonSerializerSettings {
				ContractResolver = new CamelCasePropertyNamesContractResolver()
			};
			while (count < 30) {
				try {
					Log.Debug("Checking activation...");
					var activated = await dev.CheckAuthAsync(data);
					Log.Debug("Checked...");
					if (!string.IsNullOrEmpty(activated.Token)) {
						Log.Information("Device is activated!");
						await DataUtil.AddDeviceAsync(activated, true);
						if (clientProxy != null) {
							await clientProxy.SendAsync("auth", "authorized"); }
						await _hubContext.Clients.All.SendAsync("device", JsonConvert.SerializeObject((IColorTargetData)data, serializerSettings)).ConfigureAwait(false);
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

				await clientProxy.SendAsync("auth", "update", count).ConfigureAwait(false);
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


	public async Task FlashLed(int led) {
		await FlashLedEvent.InvokeAsync(this, new DynamicEventArgs(led));
	}
	
	public async Task FlashDevice(string deviceId) {
		await FlashDeviceEvent.InvokeAsync(this, new DynamicEventArgs(deviceId));
	}

	public async Task FlashSector(int sector) {
		await FlashSectorEvent.InvokeAsync(this, new DynamicEventArgs(sector));
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
		Task.Run(async () => {
			while (!stoppingToken.IsCancellationRequested) {
				await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
			}
			_bt?.Stop();
			_ut?.Stop();
		}, stoppingToken);
		return Task.CompletedTask;
	}

	private static void CheckBackups(object? sender, ElapsedEventArgs elapsedEventArgs) {
		var userPath = SystemUtil.GetUserDir();
		var dbFiles = Directory.GetFiles(userPath, "*.db");
		DataUtil.BackupDb();
		
		Array.Sort(dbFiles);
		Array.Reverse(dbFiles);
		
		// Prune extra backups
		if (dbFiles.Length <= 8) {
			return;
		}

		foreach (var p in dbFiles) {
			var pStamp = p.Replace(userPath, "");
			pStamp = pStamp.Replace(Path.DirectorySeparatorChar.ToString(), "");
			pStamp = pStamp.Replace("store_", "");
			pStamp = pStamp.Replace(".db", "");
			var diff = DateTime.Now - DateTime.ParseExact(pStamp, "yyyyMMdd", CultureInfo.InvariantCulture);
			if (diff <= TimeSpan.FromDays(7)) {
				continue;
			}

			if (p.Equals(Path.Combine(userPath, "store.db"))) {
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
	}

	private void CheckUpdate(object? sender, ElapsedEventArgs elapsedEventArgs) {
		if (_sd.AutoUpdate) SystemUtil.Update();
	}

	private void ShowHeader() {
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
	}

	private void Initialize() {
		Log.Information("Initializing control service...");
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

		await Task.FromResult(true);
		Log.Information("Control service stopped.");
		Log.Information("Glimmr is now terminated. Bye!");
		Log.Information("");
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

		_ut?.Stop();
		_ut = new Timer();
		_ut.Interval = 1000 * 60 * 60 * _sd.AutoUpdateTime;
		_ut.Elapsed += CheckUpdate;
		_ut.Start();
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
		await DataUtil.AddDeviceAsync(device, merge);
		await RefreshDevice(device.Id);
	}

	public async Task SendImage(string method, Mat image) {
		var vb = new VectorOfByte();
		CvInvoke.Imencode(".png", image, vb);
		await _hubContext.Clients.All.SendAsync(method, vb.ToArray());
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