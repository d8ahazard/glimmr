using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Emgu.Util;
using Glimmr.Hubs;
using Glimmr.Models.Util;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;

namespace Glimmr.Services {
	public class StatService : BackgroundService
	{
		private readonly IHubContext<SocketServer> _hubContext;
		public StatService(IHubContext<SocketServer> hubContext)
		{
			_hubContext = hubContext;
		}
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested) {
				// If not linux, do nothing
				if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) continue;
				// Get cpu data
				var cd = CpuUtil.GetStats();
				// Send it to everybody
				await _hubContext.Clients.All.SendAsync("cpuData", cd, stoppingToken);
				// Sleep for 5s
				await Task.Delay(5000, stoppingToken);
			}            
		}
	}
}