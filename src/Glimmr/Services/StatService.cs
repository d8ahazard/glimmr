#region

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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

		public StatService(IHubContext<SocketServer> hubContext, ControlService cs) {
			_hubContext = hubContext;
			_colorService = cs.ColorService;
			cs.SetModeEvent += Mode;
		}


		private Task Mode(object arg1, DynamicEventArgs arg2) {
			return _hubContext.Clients.All
				.SendAsync("frames", _colorService.Counter.Rates, CancellationToken.None);
		}

		protected override Task ExecuteAsync(CancellationToken stoppingToken) {
			Initialize();
			var watch = new Stopwatch();
			watch.Start();
			var loaded = false;
			return Task.Run(async () => {

				while (!stoppingToken.IsCancellationRequested) {
					try {
						if (loaded && watch.Elapsed <= TimeSpan.FromSeconds(5)) {
							continue;
						}

						loaded = true;
						var cd = await CpuUtil.GetStats();
						cd.Fps = _colorService.Counter.Rates;
						_colorService.ControlService.Stats = cd;
						await _hubContext.Clients.All.SendAsync("stats", cd, stoppingToken);
						watch.Restart();

					} catch (Exception e) {
						if (!e.Message.Contains("canceled")) {
							Log.Warning("Exception during init: " + e.Message + " at " + e.StackTrace);
						}
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