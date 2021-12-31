#region

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

		//var tr1 = new TextWriterTraceListener(Console.Out);
		//Trace.Listeners.Add(tr1);
		LogSwitch = new LoggingLevelSwitch {
			MinimumLevel = LogEventLevel.Debug
		};
		var lc = new LoggerConfiguration()
			.Enrich.WithCaller()
			.MinimumLevel.ControlledBy(LogSwitch)
			.WriteTo.Console(outputTemplate: outputTemplate, theme: SystemConsoleTheme.Literate)
			.Filter.ByExcluding(c => c.Properties["Caller"].ToString().Contains("SerilogLogger"))
			.Enrich.FromLogContext()
			.WriteTo.Async(a =>
				a.File(logPath, rollingInterval: RollingInterval.Day, outputTemplate: outputTemplate))
			.WriteTo.SocketSink();

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
				services.Configure<HostOptions>(hostOptions => {
					hostOptions.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
				});
				services.AddSignalR();
				services.AddSingleton<IHostedService, ControlService>();
				Log.Debug("Config...");
			})
			.ConfigureWebHostDefaults(webBuilder => {
				webBuilder.UseStartup<Startup>();
				webBuilder.UseUrls("http://*");
			});
	}
}