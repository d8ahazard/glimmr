#region

using System.IO;
using System.Runtime.InteropServices;
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

namespace Glimmr {
	public static class Program {
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

			var branch = SystemUtil.GetBranch();

			//var tr1 = new TextWriterTraceListener(Console.Out);
			//Trace.Listeners.Add(tr1);
			var lc = new LoggerConfiguration()
				.Enrich.WithCaller()
				.MinimumLevel.Information()
				.WriteTo.Console(outputTemplate: outputTemplate, theme: SystemConsoleTheme.Literate)
				.MinimumLevel.Override("Microsoft", LogEventLevel.Information)
				.Enrich.FromLogContext()
				.WriteTo.Async(a =>
					a.File(logPath, rollingInterval: RollingInterval.Day, outputTemplate: outputTemplate));
			if (branch != "master") {
				lc.MinimumLevel.Debug();
			}

			Log.Logger = lc.CreateLogger();
			CreateHostBuilder(args).Build().Run();
			Log.CloseAndFlush();
		}

		private static IHostBuilder CreateHostBuilder(string[] args) {
			return Host.CreateDefaultBuilder(args)
				.UseSerilog()
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
}