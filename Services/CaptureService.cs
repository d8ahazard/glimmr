using System;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Hubs;
using Glimmr.Models.Util;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Q42.HueApi;

namespace Glimmr.Services {
	// Handles capturing and sending color data
	public class CaptureService : BackgroundService {
		private IHubContext<SocketServer> _hubContext;
		private readonly ControlService _controlService;
		
		public CaptureService(IHubContext<SocketServer> hubContext, ControlService controlService) {
			_hubContext = hubContext;
			_controlService = controlService;
			LogUtil.Write("Initialisation complete.");
		}
		
		protected override Task ExecuteAsync(CancellationToken stoppingToken) {
			// If our control service says so, refresh data on all devices while streaming
			_controlService.TriggerRefresh += RefreshDevices;
			_controlService.SetMode += Mode;
			while (!stoppingToken.IsCancellationRequested) {
				
			}
			return Task.CompletedTask;
		}

		private void RefreshDevices() {
			
		}

		private void Mode(int newMode) {
			
		}
	}

}