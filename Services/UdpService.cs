using System.Threading;
using System.Threading.Tasks;
using Glimmr.Hubs;
using Glimmr.Models.Util;
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
			while (!stoppingToken.IsCancellationRequested) {
				
			}
			return Task.CompletedTask;
		}
	}
}