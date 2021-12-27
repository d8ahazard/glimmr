#region

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
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

#endregion

namespace Glimmr; 

public static class Program {
	public static void Main(string[] args) {
		const string outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}]{Caller} {Message}{NewLine}{Exception}";
		var logPath = "/var/log/glimmr/glimmr.log";
		var sd = DataUtil.GetSystemData();
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
		var lc = new LoggerConfiguration()
			.Enrich.WithCaller()
			.MinimumLevel.Information()
			.WriteTo.Console(outputTemplate: outputTemplate, theme: SystemConsoleTheme.Literate)
			.MinimumLevel.Override("Microsoft", LogEventLevel.Information)
			.Filter.ByExcluding(c => c.Properties["Caller"].ToString().Contains("SerilogLogger"))
			.Enrich.FromLogContext()
			.WriteTo.Async(a =>
				a.File(logPath, rollingInterval: RollingInterval.Day, outputTemplate: outputTemplate))
			.WriteTo.SocketSink();

		if (sd.LogLevel == 0) {
			lc.MinimumLevel.Debug();
		}

		Log.Logger = lc.CreateLogger();
		CreateHostBuilder(args, Log.Logger).Build().Run();
		Log.CloseAndFlush();
	}

	private static IHostBuilder CreateHostBuilder(string[] args, ILogger logger) {
		return Host.CreateDefaultBuilder(args)
			.UseSerilog(logger)
			.ConfigureServices(services => {
				services.AddSignalR();
				services.AddSingleton<ControlService>();
				services.AddHostedService<ColorService>();
				services.AddHostedService<DiscoveryService>();
				services.AddHostedService<StatService>();
			})
			.ConfigureWebHostDefaults(webBuilder => {
				webBuilder.UseStartup<Startup>();
				webBuilder.UseUrls("http://*");
			});
	}
}