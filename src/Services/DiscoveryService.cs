using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Glimmr.Hubs;
using Glimmr.Models.Util;
using LifxNet;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Serilog;
using Timer = System.Timers.Timer;

namespace Glimmr.Services {
	public class DiscoveryService : BackgroundService {
		private readonly IHubContext<SocketServer> _hubContext;
		// CONTROL SERVICE CONTROLS ALL
		private readonly ControlService _controlService;
		private Timer _refreshTimer;
		private LifxClient _lifxClient;
		private bool _isStreaming;
		public DiscoveryService(IHubContext<SocketServer> hubContext, ControlService controlService) {
			_hubContext = hubContext;
			_controlService = controlService;
			_controlService.DeviceRescanEvent += TriggerRefresh;
			_controlService.SetModeEvent += ToggleAutoScan;
			_lifxClient = _controlService.LifxClient;
		}

		protected override Task ExecuteAsync(CancellationToken stoppingToken) {
			return Task.Run(async () => {
				Log.Debug("Starting discovery service loop.");
				StartRefreshTimer();
				while (!stoppingToken.IsCancellationRequested) {
					await Task.Delay(1, stoppingToken);
				}
				return Task.CompletedTask;
			}, stoppingToken);
		}

		public override Task StopAsync(CancellationToken cancellationToken) {
			Log.Debug("Stopping discovery service...");
			_refreshTimer.Close();
			Log.Debug("Discovery service stopped.");
			return base.StopAsync(cancellationToken);
		}

		private void ToggleAutoScan(int mode) {
			_isStreaming = mode != 0;
		}
		
		private void StartRefreshTimer(bool refreshNow = false) {
			// Option to refresh immediately on execute
			if (refreshNow) {
				
			}
            
			// Reset and restart our timer
			_refreshTimer = new Timer(600000);
			_refreshTimer.Elapsed += DeviceDiscovery;
			_refreshTimer.AutoReset = true;
			_refreshTimer.Enabled = true;
		}

		private void TriggerRefresh() {
			Log.Debug("Triggering refresh.");
			DeviceDiscovery();
		}
        
		// Discover...devices?
		private void DeviceDiscovery(object sender, ElapsedEventArgs elapsedEventArgs) {
			if (!_isStreaming) {
				DeviceDiscovery();
			}
		}
		
		private async void DeviceDiscovery() {
			Log.Debug("Triggering refresh of devices via timer.");
			// Trigger a refresh
			_lifxClient = _controlService.LifxClient;
			DataUtil.RefreshDevices(_lifxClient, _controlService);
			// Sleep for 5s for it to finish
			Thread.Sleep(5000);
			// Notify all clients to refresh data
			await _hubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
			Log.Debug("Discovery done.");
		}
	}
}