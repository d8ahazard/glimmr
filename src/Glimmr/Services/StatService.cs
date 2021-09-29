#region

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Hubs;
using Glimmr.Models;
using Glimmr.Models.Util;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Serilog;

#endregion

namespace Glimmr.Services {
	public class StatService : BackgroundService {
		private readonly ColorService _colorService;
		private readonly IHubContext<SocketServer> _hubContext;
		private int _count;

		public StatService(IHubContext<SocketServer> hubContext, ControlService cs) {
			_hubContext = hubContext;
			_colorService = cs.ColorService;
			cs.SetModeEvent += Mode;
			_count = 0;
		}


		private Task Mode(object arg1, DynamicEventArgs arg2) {
			return _hubContext.Clients.All
				.SendAsync("frames", _colorService.Counter.Rates, CancellationToken.None);
		}

		protected override Task ExecuteAsync(CancellationToken stoppingToken) {
			Initialize();
			return Task.Run(async () => {
				try {
					while (!stoppingToken.IsCancellationRequested) {
						// Sleep for 5s
						await Task.Delay(5000, stoppingToken);
						if (_count >= 6) {
							_count = 0;
							if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
								continue;
							}

							var cd = await CpuUtil.GetStats();
							// Send it to everybody

							await _hubContext.Clients.All.SendAsync("cpuData", cd, stoppingToken);
							_count = 0;
						}

						if (_colorService.DeviceMode != DeviceMode.Off) {
							await _hubContext.Clients.All
								.SendAsync("frames", _colorService.Counter.Rates, CancellationToken.None)
								.ConfigureAwait(false);
						}

						_count++;
					}
				} catch (Exception e) {
					if (!e.Message.Contains("canceled")) {
						Log.Warning("Exception during init: " + e.Message);
					}
				}

				Log.Information("Stat service stopped.");
				return Task.CompletedTask;
			}, stoppingToken);
		}

		private static void Initialize() {
			Log.Debug("Starting stat service...");
			Log.Debug("Stat service started.");
		}
	}
}