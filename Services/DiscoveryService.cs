using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Glimmr.Hubs;
using Glimmr.Models.Util;
using LifxNet;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Timer = System.Timers.Timer;

namespace Glimmr.Services {
	public class DiscoveryService : BackgroundService {
		private IHubContext<SocketServer> _hubContext;
		// CONTROL SERVICE CONTROLS ALL
		private readonly ControlService _controlService;
		private Timer _refreshTimer;
		private LifxClient _lifxClient;
		private bool isStreaming;
		public DiscoveryService(IHubContext<SocketServer> hubContext, ControlService controlService) {
			_hubContext = hubContext;
			_controlService = controlService;
			_controlService.RescanDeviceEvent += TriggerRefresh;
			_controlService.SetModeEvent += ToggleAutoScan;
			_lifxClient = _controlService.LifxClient;
		}

		protected override Task ExecuteAsync(CancellationToken stoppingToken) {
			return Task.Run(async () => {
				LogUtil.Write("Starting discovery service loop.");
				StartRefreshTimer();
				while (!stoppingToken.IsCancellationRequested) {
					await Task.Delay(1);
				}

				_refreshTimer.Close();
			});
		}

		private void ToggleAutoScan(int mode) {
			isStreaming = mode != 0;
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
			DeviceDiscovery(null, null);
		}
        
		// Discover...devices?
		private void DeviceDiscovery(object sender, ElapsedEventArgs elapsedEventArgs) {
			if (!isStreaming) {
				DeviceDiscovery();
			}
		}
		
		private async void DeviceDiscovery() {
			LogUtil.Write("Triggering refresh of devices via timer.");
			// Trigger a refresh
			_lifxClient = _controlService.LifxClient;
			DataUtil.RefreshDevices(_lifxClient);
			// Sleep for 5s for it to finish
			Thread.Sleep(5000);
			// Notify all clients to refresh data
			await _hubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
		}
	}
}