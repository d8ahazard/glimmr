#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models;
using Glimmr.Models.ColorTarget;
using Glimmr.Models.Util;
using Microsoft.Extensions.Hosting;
using Serilog;

#endregion

namespace Glimmr.Services {
	public class DiscoveryService : BackgroundService {
		private readonly List<IColorDiscovery> _discoverables;
		private readonly CancellationTokenSource _syncSource;
		private bool _discovering;
		private int _discoveryInterval;
		private CancellationTokenSource? _mergeSource;
		private CancellationToken _stopToken;
		private bool _streaming;

		public DiscoveryService(ControlService cs) {
			cs.DeviceRescanEvent += TriggerRefresh;
			cs.SetModeEvent += UpdateMode;
			cs.RefreshSystemEvent += RefreshSystem;
			_discoveryInterval = DataUtil.GetItem<int>("AutoDiscoveryFrequency");
			if (_discoveryInterval < 15) {
				_discoveryInterval = 15;
			}

			var classnames = SystemUtil.GetClasses<IColorDiscovery>();
			_discoverables = new List<IColorDiscovery>();
			_syncSource = new CancellationTokenSource();
			foreach (var dev in classnames.Select(c => Activator.CreateInstance(Type.GetType(c)!, cs.ColorService))
				         .Where(obj => obj != null).Cast<IColorDiscovery?>()) {
				if (dev != null) {
					_discoverables.Add(dev);
				}
			}
		}

		private void RefreshSystem() {
			_syncSource.Cancel();
			_mergeSource = CancellationTokenSource.CreateLinkedTokenSource(_syncSource.Token, _stopToken);
			_discoveryInterval = DataUtil.GetItem<int>("AutoDiscoveryFrequency");
			if (_discoveryInterval < 15) {
				_discoveryInterval = 15;
			}
		}

		protected override Task ExecuteAsync(CancellationToken stoppingToken) {
			_mergeSource = Initialize(stoppingToken);
			return Task.Run(async () => {
				while (!stoppingToken.IsCancellationRequested) {
					await Task.Delay(TimeSpan.FromMinutes(_discoveryInterval), _mergeSource.Token);
					if (!_streaming) {
						await TriggerRefresh(this, null);
					}
				}

				return Task.CompletedTask;
			}, stoppingToken);
		}

		private CancellationTokenSource Initialize(CancellationToken stoppingToken) {
			Log.Information($"Starting discovery service, interval is {_discoveryInterval} seconds...");
			_stopToken = stoppingToken;
			var devs = DataUtil.GetDevices();
			if (devs.Count == 0 || SystemUtil.IsRaspberryPi() && devs.Count == 2) {
				Log.Debug($"Dev count is {devs.Count}, scanning...");
				TriggerRefresh(null, null).ConfigureAwait(false);
			}

			Log.Information("Discovery service started.");
			return CancellationTokenSource.CreateLinkedTokenSource(_syncSource.Token, _stopToken);
		}

		public override Task StopAsync(CancellationToken cancellationToken) {
			Log.Information("Discovery service stopped.");
			return base.StopAsync(cancellationToken);
		}

		private Task UpdateMode(object o, DynamicEventArgs dynamicEventArgs) {
			_streaming = dynamicEventArgs.Arg0 != 0;
			return Task.CompletedTask;
		}


		private async Task TriggerRefresh(object? o, DynamicEventArgs? dynamicEventArgs) {
			Log.Information("Beginning device discovery...");
			var cs = new CancellationTokenSource();
			var sd = DataUtil.GetSystemData();
			var timeout = sd.DiscoveryTimeout;
			if (timeout < 3) {
				timeout = 3;
			}

			cs.CancelAfter(TimeSpan.FromSeconds(timeout));
			await DeviceDiscovery(timeout, cs.Token);
			var devs = DataUtil.GetDevices();
			if (!sd.AutoRemoveDevices) {
				return;
			}

			foreach (var dev in devs) {
				var device = (IColorTargetData)dev;
				var lastSeen = DateTime.Parse(device.LastSeen, CultureInfo.InvariantCulture);
				if (DateTime.Now - lastSeen < TimeSpan.FromDays(sd.AutoRemoveDevicesAfter)) {
					continue;
				}

				bool online = SystemUtil.IsOnline(dev.IpAddress);
				if (online) {
					dev.LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
					DataUtil.AddDeviceAsync(dev);
				}

				DataUtil.DeleteDevice(device.Id);
			}

			cs.Dispose();
			Log.Information("Device discovery completed.");
		}


		private async Task DeviceDiscovery(int timeout, CancellationToken token) {
			if (_discovering) {
				return;
			}

			_discovering = true;
			var tasks = _discoverables
				.Select(disco => Task.Run(() => disco.Discover(token, timeout), CancellationToken.None)).ToList();

			try {
				await Task.WhenAll(tasks);
			} catch (Exception e) {
				Log.Warning($"Exception during discovery: {e.Message}" + e.StackTrace);
			} finally {
				foreach (var task in tasks) {
					task.Dispose();
				}
			}

			_discovering = false;
		}
	}
}