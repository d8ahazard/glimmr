#region

using System.IO;
using System.Runtime.InteropServices;
using Glimmr.Models.ColorSource.Ambient;
using Glimmr.Models.ColorSource.Audio;
using Glimmr.Models.ColorSource.AudioVideo;
using Glimmr.Models.ColorSource.DreamScreen;
using Glimmr.Models.ColorSource.Video;
using Glimmr.Models.Logging;
using Glimmr.Models.Util;
using Glimmr.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
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

			//var tr1 = new TextWriterTraceListener(Console.Out);
			//Trace.Listeners.Add(tr1);

			Log.Logger = new LoggerConfiguration()
				.Enrich.WithCaller()
				.WriteTo.Console(outputTemplate: outputTemplate, theme: SystemConsoleTheme.Literate)
				.MinimumLevel.Information()
				.MinimumLevel.Override("Microsoft", LogEventLevel.Information)
				.Enrich.FromLogContext()
				.WriteTo.Async(a =>
					a.File(logPath, rollingInterval: RollingInterval.Day, outputTemplate: outputTemplate))
				.CreateLogger();

			CreateHostBuilder(args).Build().Run();
			Log.CloseAndFlush();
		}

		private static IHostBuilder CreateHostBuilder(string[] args) {
			var outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}]{Caller} {Message}{NewLine}{Exception}";
			var logPath = "/var/log/glimmr/glimmr.log";
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				var userPath = SystemUtil.GetUserDir();
				var logDir = Path.Combine(userPath, "log");
				if (!Directory.Exists(logDir)) {
					Directory.CreateDirectory(logDir);
				}

				logPath = Path.Combine(userPath, "log", "glimmr.log");
			}

			return Host.CreateDefaultBuilder(args)
				.UseSerilog((_, loggerConfiguration) => loggerConfiguration
					.Enrich.WithCaller()
					.MinimumLevel.Information()
					.Enrich.FromLogContext()
					.Filter.ByExcluding(c => JsonConvert.SerializeObject(c).Contains("SerilogLogger"))
					.WriteTo.Console(outputTemplate: outputTemplate)
					.WriteTo.Async(a =>
						a.File(logPath, rollingInterval: RollingInterval.Day, outputTemplate: outputTemplate)))
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