﻿#region

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Hubs;
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

		public StatService(IHubContext<SocketServer> hubContext, ColorService colorService) {
			_hubContext = hubContext;
			_colorService = colorService;
			_count = 0;
		}

		protected override Task ExecuteAsync(CancellationToken stoppingToken) {
			Log.Debug("StatService initialized.");
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

						_count++;
						if (_colorService.DeviceMode != DeviceMode.Off) {
							await _hubContext.Clients.All
								.SendAsync("frames", _colorService.Counter.Rates(), stoppingToken)
								.ConfigureAwait(false);
						}
					}
				} catch (Exception e) {
					Log.Warning("Exception during init: " + e.Message);
				}

				Log.Information("Stopped stat service.");
				return Task.CompletedTask;
			}, stoppingToken);
		}
	}
}