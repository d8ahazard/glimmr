using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Hubs;
using Glimmr.Models.Util;
using LifxNet;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;

namespace Glimmr.Services {
	public class UdpService : BackgroundService {
		
		private IHubContext<SocketServer> _hubContext;
		private readonly ControlService _controlService;
		
		public UdpService(IHubContext<SocketServer> hubContext, ControlService controlService) {
			_hubContext = hubContext;
			_controlService = controlService;
			

			if (_controlService != null) {
				LogUtil.Write("looks like control service is working!");
			}
			LogUtil.Write("Initialisation complete.");
		}

		
		protected override Task ExecuteAsync(CancellationToken stoppingToken) {
			return Task.Run(async () => {
				LogUtil.Write("Starting UDP Service loop.");
				while (!stoppingToken.IsCancellationRequested) {
					await Task.Delay(1, stoppingToken);
				}
			});
		}
	}
}