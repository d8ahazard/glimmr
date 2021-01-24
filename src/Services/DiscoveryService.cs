using System.Threading;
using System.Threading.Tasks;
using Glimmr.Hubs;
using Glimmr.Models.ColorTarget;
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
					await Task.Delay(600000, stoppingToken);
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

		private void UpdateMode(int mode) {
			_streaming = mode != 0;
		}


		private void TriggerRefresh() {
			Log.Debug("Triggering refresh.");
			DeviceDiscovery();
		}
        
		// Discover...devices?
		
		private async void DeviceDiscovery() {
			Log.Debug("Triggering refresh of devices via timer.");
			// Trigger a refresh
			_lifxClient = _controlService.LifxClient;
			DataUtil.RefreshDevices(_lifxClient, _controlService);
			// Sleep for 5s for it to finish
			await Task.Delay(5000);
			// Notify all clients to refresh data
			await _hubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
		}
	}
}