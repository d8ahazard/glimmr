#region

using System;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Hubs;
using Glimmr.Models.Helper;
using Glimmr.Models.Util;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Serilog;

#endregion

namespace Glimmr.Services;

public class StatService : BackgroundService {
	private readonly ColorService _colorService;
	private readonly IHubContext<SocketServer> _hubContext;

	public StatService(IHubContext<SocketServer> hubContext) {
		var cs = ControlService.GetInstance();
		_hubContext = hubContext;
		_colorService = cs.ColorService;
		cs.SetModeEvent += Mode;
	}


	private Task Mode(object arg1, DynamicEventArgs arg2) {
		return _hubContext.Clients.All
			.SendAsync("frames", _colorService.Counter.Rates, CancellationToken.None);
	}

	protected override Task ExecuteAsync(CancellationToken stoppingToken) {
		Log.Debug("Starting stat service...");
		var statTask = Task.Run(async () => {
			while (!stoppingToken.IsCancellationRequested) {
				try {
					await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
					var cd = await StatUtil.GetStats();
					cd.Fps = _colorService.Counter.Rates;
					_colorService.ControlService.Stats = cd;
					await _hubContext.Clients.All.SendAsync("stats", cd, stoppingToken);
				} catch (Exception e) {
					if (!e.Message.Contains("canceled")) {
						Log.Warning("Exception during init: " + e.Message + " at " + e.StackTrace);
					}
				}
			}

			Log.Information("Stat service stopped.");
		}, CancellationToken.None);
		Log.Debug("Stat service started...");
		return statTask;
	}
}