using System.Diagnostics;
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
using Serilog;
using Serilog.Events;

namespace Glimmr
{
	public class Program
	{
		public static void Main(string[] args) {
			var outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}]{Caller} {Message}{NewLine}{Exception}";
			var logPath = "/var/log/glimmr/glimmr.log";
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				var userPath = SystemUtil.GetUserDir();
				var logDir = Path.Combine(userPath, "log");
				if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
				logPath = Path.Combine(userPath, "log", "glimmr.log");
			}
			
			var tr1 = new TextWriterTraceListener(System.Console.Out);
			//Trace.Listeners.Add(tr1);

			Log.Logger = new LoggerConfiguration()
				.Enrich.WithCaller()
				.WriteTo.Console(outputTemplate: outputTemplate)
				.MinimumLevel.Debug()
				.MinimumLevel.Override("Microsoft", LogEventLevel.Information)
				.Enrich.FromLogContext()
				.WriteTo.File(logPath, rollingInterval: RollingInterval.Day, outputTemplate: outputTemplate)
				.CreateLogger();
            
			CreateHostBuilder(args).Build().Run();
		}

		private static IHostBuilder CreateHostBuilder(string[] args) {
			return Host.CreateDefaultBuilder(args)
				.UseSerilog()
				.ConfigureServices(services => {
					services.AddSignalR();
					services.AddSingleton<ControlService>();
					services.AddSingleton<ColorService>();
					services.AddHostedService(services => (ColorService) services.GetService<ColorService>());
					services.AddHostedService<AudioStream>();
					services.AddHostedService<VideoStream>();
					services.AddHostedService<AudioVideoStream>();
					services.AddHostedService<AmbientStream>();
					services.AddHostedService<DreamScreenStream>();
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