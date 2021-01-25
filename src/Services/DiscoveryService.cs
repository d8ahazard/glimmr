using System;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Hubs;
using Glimmr.Models;
using Glimmr.Models.Util;
using LifxNet;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Glimmr.Services {
	public class DiscoveryService : BackgroundService {
		private readonly IHubContext<SocketServer> _hubContext;
		// CONTROL SERVICE CONTROLS ALL
		private readonly ControlService _controlService;
		private LifxClient _lifxClient;
		private bool _streaming;
		public DiscoveryService(IHubContext<SocketServer> hubContext, ControlService controlService) {
			_hubContext = hubContext;
			_controlService = controlService;
			_controlService.DeviceRescanEvent += TriggerRefresh;
			_controlService.SetModeEvent += UpdateMode;
			_lifxClient = _controlService.LifxClient;
		}

		protected override Task ExecuteAsync(CancellationToken stoppingToken) {
			return Task.Run(async () => {
				Log.Debug("Starting discovery service loop.");
				
				while (!stoppingToken.IsCancellationRequested) {
					await Task.Delay(TimeSpan.FromMinutes(60), stoppingToken);
					Log.Debug("Auto-refreshing devices...");
					if (!_streaming) TriggerRefresh();
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


		private void TriggerRefresh() {
			Log.Debug("Triggering refresh.");
			DeviceDiscovery();
		}
        
		// Discover...devices?
		
		private async Task DeviceDiscovery() {
			Log.Debug("Triggering refresh of devices via timer.");
			// Trigger a refresh
			_lifxClient = _controlService.LifxClient;
			await DataUtil.RefreshDevices(_lifxClient, _controlService);
			// Sleep for 5s for it to finish
			await Task.Delay(5000);
			// Notify all clients to refresh data
			await _hubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
		}
	}
}