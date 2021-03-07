using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Hubs;
using Glimmr.Models.Util;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Glimmr.Services {
	public class StatService : BackgroundService
	{
		private readonly IHubContext<SocketServer> _hubContext;
		public StatService(IHubContext<SocketServer> hubContext)
		{
			_hubContext = hubContext;
		}
		protected override Task ExecuteAsync(CancellationToken stoppingToken) {
			Log.Debug("StatService initialized.");
			return Task.Run(async () => {
				try {
					while (!stoppingToken.IsCancellationRequested) {
						// If not linux, do nothing
						if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return Task.CompletedTask;
						// Get cpu data
						var cd = await CpuUtil.GetStats();
						// Send it to everybody
						await _hubContext.Clients.All.SendAsync("cpuData", cd, stoppingToken);
						// Sleep for 5s
						await Task.Delay(30000, stoppingToken);
					}
				} catch (Exception e) {
					Log.Debug("Exception during init: " + e.Message);
				}

				Log.Debug("Stopped stat service.");
				return Task.CompletedTask;
			}, stoppingToken);
		}
	}
}