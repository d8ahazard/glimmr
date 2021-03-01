using System.Diagnostics;
using System.Runtime.InteropServices;
using Glimmr.Models.ColorSource.Ambient;
using Glimmr.Models.ColorSource.Audio;
using Glimmr.Models.ColorSource.AudioVideo;
using Glimmr.Models.ColorSource.Video;
using Glimmr.Models.Logging;
using Glimmr.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Glimmr
{
	public class Program
	{
		public static void Main(string[] args) {
			var outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}]{Caller} {Message}{NewLine}{Exception}";

			var logPath = "/var/log/glimmr/glimmr.log";
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				logPath = "log\\glimmr.log";
			}
			
			// var tr1 = new TextWriterTraceListener(System.Console.Out);
			// Trace.Listeners.Add(tr1);

			Log.Logger = new LoggerConfiguration()
				.Enrich.WithCaller()
				.WriteTo.Console(outputTemplate: outputTemplate)
				.MinimumLevel.Debug()
				.WriteTo.File(logPath, rollingInterval: RollingInterval.Hour, outputTemplate: outputTemplate)
				.CreateLogger();
            
			CreateHostBuilder(args).Build().Run();
		}

		private static IHostBuilder CreateHostBuilder(string[] args) {
			return Host.CreateDefaultBuilder(args)
				.ConfigureLogging((hostingContext, logging) => {
					logging.ClearProviders();
					logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
					logging.AddConsole();
					logging.AddDebug();
				})
				// Initialize our dream screen emulator
				.ConfigureServices(services => {
					services.AddSignalR();
					services.AddSingleton<ControlService>();
					services.AddSingleton<ColorService>();
					services.AddHostedService(services => (ColorService) services.GetService<ColorService>());
					services.AddHostedService<AudioStream>();
					services.AddHostedService<VideoStream>();
					services.AddHostedService<AudioVideoStream>();
					services.AddHostedService<AmbientStream>();
					services.AddHostedService<DreamService>();
					services.AddHostedService<StreamService>();
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