#region

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Glimmr.Hubs;
using Glimmr.Models.Logging;
using Glimmr.Models.Util;
using Glimmr.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

#endregion

namespace Glimmr;

public static class Program {
	public static LoggingLevelSwitch? LogSwitch { get; private set; }

	public static void Main(string[] args) {
		const string outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}]{Caller} {Message}{NewLine}{Exception}";
		var logPath = "/var/log/glimmr/glimmr.log";
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
			var userPath = SystemUtil.GetUserDir();
			var logDir = Path.Combine(userPath, "log");
			if (!Directory.Exists(logDir)) {
				Directory.CreateDirectory(logDir);
			}

			logPath = Path.Combine(userPath, "log", "glimmr.log");
		}

		var tr1 = new TextWriterTraceListener(Console.Out);
		Trace.Listeners.Add(tr1);
		LogSwitch = new LoggingLevelSwitch {
			MinimumLevel = LogEventLevel.Debug
		};
		var lc = new LoggerConfiguration()
			.Enrich.WithCaller()
			.MinimumLevel.ControlledBy(LogSwitch)
			//.WriteTo.Console(outputTemplate: outputTemplate, theme: SystemConsoleTheme.Literate)
			.Filter.ByExcluding(c => c.Properties["Caller"].ToString().Contains("SerilogLogger"))
			.Enrich.FromLogContext()
			.WriteTo.Async(a =>
				a.File(
					logPath,
					rollingInterval: RollingInterval.Day,
					outputTemplate: outputTemplate,
					flushToDiskInterval: TimeSpan.FromSeconds(1)
				)
			)
			.WriteTo.Async(a => a.SocketSink())
			.WriteTo.Async(a => a.Console(outputTemplate: outputTemplate, theme: SystemConsoleTheme.Literate));

		Log.Logger = lc.CreateLogger();

		var app = Process.GetCurrentProcess().MainModule;
		if (app != null) {
			var file = app.FileName;
			var appProcessName = Path.GetFileNameWithoutExtension(file);
			var runningProcesses = Process.GetProcessesByName(appProcessName);
			if (runningProcesses.Length > 1) {
				Log.Information("Glimmr is already running, exiting.");
				Log.CloseAndFlush();
				return;
			}
		}

		CreateHostBuilder(args, Log.Logger).Build().Run();
		//ControlService.LevelSwitch = levelSwitch;
		Log.CloseAndFlush();
	}

	private static IHostBuilder CreateHostBuilder(string[] args, ILogger logger) {
		return Host.CreateDefaultBuilder(args)
			.UseDefaultServiceProvider(o => { o.ValidateOnBuild = false; })
			.UseSerilog(logger)
			.UseConsoleLifetime()
			.ConfigureServices(services => {
				services.AddSignalR(o => {
					// Generous timeouts: settings changes can trigger heavy refresh work,
					// and throttled/background tabs ping slowly. Don't drop clients for it.
					o.KeepAliveInterval = TimeSpan.FromSeconds(10);
					o.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
					// Allow a few overlapping invocations so a slow call (e.g. device scan)
					// doesn't queue up subsequent settings commits behind it.
					o.MaximumParallelInvocationsPerClient = 3;
				});
				services.Configure<HostOptions>(hostOptions => {
					hostOptions.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
				});
			})
			.ConfigureWebHostDefaults(webBuilder => {
				var path = Path.Combine(AppContext.BaseDirectory, "wwwroot");
				if (Directory.Exists(path)) {
					webBuilder.UseContentRoot(AppContext.BaseDirectory);
				}

				webBuilder.UseStartup<Startup>();
				webBuilder.ConfigureKestrel(options => {
					// Standard Glimmr web UI / API over HTTP.
					options.ListenAnyIP(80);

					// HTTPS endpoint required for the emulated Hue Bridge so the Hue Sync app
					// (which expects a TLS bridge for the CLIP v2 API) can pair and stream to us.
					try {
						var cert = Glimmr.Models.ColorSource.HueSync.HueCertificateFactory.CreateOrLoad();
						options.ListenAnyIP(443, listenOptions => listenOptions.UseHttps(cert));
					} catch (Exception e) {
						Log.Warning("Unable to bind HTTPS/443 for Hue Sync bridge emulation: " + e.Message);
					}
				});
			})
			.ConfigureServices(services => {
				services.AddSingleton<IHostedService, ControlService>();
				services.AddHostedService<ColorService>();
				services.AddHostedService<StatService>();
				services.AddHostedService<DiscoveryService>();
				services.AddSingleton<RoomMapService>();
				services.AddSingleton<CalibrationService>();
				services.AddSingleton<CalibrationRtcService>();
				services.AddSingleton<OutputSchedulerService>();
			});
	}
}