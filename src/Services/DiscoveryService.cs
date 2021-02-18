using System;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Hubs;
using Glimmr.Models;
using Glimmr.Models.ColorTarget.Hue;
using Glimmr.Models.ColorTarget.LIFX;
using Glimmr.Models.ColorTarget.Nanoleaf;
using Glimmr.Models.ColorTarget.Wled;
using Glimmr.Models.ColorTarget.Yeelight;
using Glimmr.Models.Util;
using ISocketLite.PCL.Exceptions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Glimmr.Services {
	public class DiscoveryService : BackgroundService {
		private readonly IHubContext<SocketServer> _hubContext;
		private readonly ControlService _controlService;
		private bool _streaming;
		private readonly YeelightDiscovery _yeelightDiscovery;
		private readonly WledDiscovery _wledDiscovery;
		private readonly NanoDiscovery _nanoDiscovery;
		private readonly LifxDiscovery _lifxDiscovery;
		private readonly HueDiscovery _hueDiscovery;
		public DiscoveryService(IHubContext<SocketServer> hubContext, ControlService controlService) {
			_hubContext = hubContext;
			_controlService = controlService;
			_controlService.DeviceRescanEvent += TriggerRefresh;
			_controlService.SetModeEvent += UpdateMode;
			_hueDiscovery = new HueDiscovery();
			_yeelightDiscovery = new YeelightDiscovery();
			_lifxDiscovery = new LifxDiscovery(_controlService);
			_nanoDiscovery = new NanoDiscovery(_controlService);
			_wledDiscovery = new WledDiscovery(_controlService);
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
			Log.Debug("Triggering refresh of devices via timer.");
			// Trigger a refresh
			var cs = new CancellationTokenSource();
			cs.CancelAfter(TimeSpan.FromSeconds(timeout));
			Log.Debug("Starting Device Discovery...");
			// Get dream devices
			var lifxTask = _lifxDiscovery.Discover(cs.Token);
			var nanoTask = _nanoDiscovery.Discover(cs.Token);
			var bridgeTask = _hueDiscovery.Discover(cs.Token);
			var wLedTask = _wledDiscovery.Discover(cs.Token);
			var yeeTask = _yeelightDiscovery.Discover(cs.Token);
			_controlService.RefreshDreamscreen(cs.Token);
			try {
				await Task.WhenAll(nanoTask, bridgeTask, lifxTask, wLedTask, yeeTask);
			} catch (SocketException f) {
				Log.Warning("Exception during discovery: " + f.Message);
			}
				
			Log.Debug("All devices should now be refreshed.");
			nanoTask.Dispose();
			bridgeTask.Dispose();
			wLedTask.Dispose();
			yeeTask.Dispose();
			lifxTask.Dispose();
			cs.Dispose();

			// Notify all clients to refresh data
			await _hubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized(), CancellationToken.None);
		}
	}
}