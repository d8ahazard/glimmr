using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Hubs;
using Glimmr.Models;
using Glimmr.Models.ColorTarget;
using Glimmr.Models.Util;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Serilog;

namespace Glimmr.Services {
	public class DiscoveryService : BackgroundService {
		private readonly IHubContext<SocketServer> _hubContext;
		private bool _streaming;
		private bool _discovering;
		private readonly List<IColorDiscovery> _discoverables;
		public DiscoveryService(IHubContext<SocketServer> hubContext, ColorService colorService) {
			_hubContext = hubContext;
			var controlService1 = colorService.ControlService;
			controlService1.DeviceRescanEvent += TriggerRefresh;
			controlService1.SetModeEvent += UpdateMode;
			var classnames = SystemUtil.GetClasses<IColorDiscovery>();
			_discoverables = new List<IColorDiscovery>();
			Log.Debug("Adding discovery classes...");
			foreach (var c in classnames) {
				Log.Debug("C: " + c);
				var dev = (IColorDiscovery) Activator.CreateInstance(Type.GetType(c)!, colorService);
				_discoverables.Add(dev);
			}
		}

		protected override Task ExecuteAsync(CancellationToken stoppingToken) {
			return Task.Run(async () => {
				Log.Debug("Starting discovery service loop.");
				
				while (!stoppingToken.IsCancellationRequested) {
					await Task.Delay(TimeSpan.FromMinutes(60), stoppingToken);
					Log.Debug("Auto-refreshing devices...");
					if (!_streaming) await TriggerRefresh(this, null);
				}
				return Task.CompletedTask;
			}, stoppingToken);
		}

		public override Task StopAsync(CancellationToken cancellationToken) {
			Log.Debug("Discovery service stopped.");
			return base.StopAsync(cancellationToken);
		}

		private Task UpdateMode(object o, DynamicEventArgs dynamicEventArgs) {
			_streaming = dynamicEventArgs.P1 != 0;
			return Task.CompletedTask;
		}


		private async Task TriggerRefresh(object o, DynamicEventArgs dynamicEventArgs) {
			Log.Debug("Triggering refresh.");
			await DeviceDiscovery();
		}

        
		private async Task DeviceDiscovery(int timeout=15) {
			if (_discovering) return;
			_discovering = true;
			Log.Debug("Triggering refresh of devices via timer.");
			// Trigger a refresh
			var cs = new CancellationTokenSource();
			cs.CancelAfter(TimeSpan.FromSeconds(timeout));
			Log.Debug("Starting Device Discovery...");
			var tasks = _discoverables.Select(disco => Task.Run(() =>disco.Discover(cs.Token))).ToList();

			try {
				await Task.WhenAll(tasks);
			} catch (Exception e) {
				Log.Warning("Exception during discovery: " + e.Message);
			} finally {
				foreach (var task in tasks) {
					task.Dispose();
				}
				cs.Dispose();

			}
				
			Log.Debug("All devices should now be refreshed.");
			_discovering = false;
			// Notify all clients to refresh data
			await _hubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized(), CancellationToken.None);
		}
	}
}