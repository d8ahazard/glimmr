using System.Globalization;
using System.Runtime.InteropServices;
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
			var outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message} (at {Caller}){NewLine}{Exception}";

			var logPath = "/var/log/glimmr/glimmr.log";
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				logPath = "log\\glimmr.log";
			}
			Log.Logger = new LoggerConfiguration()
				.Enrich.WithCaller()
				.WriteTo.Console(outputTemplate: outputTemplate)
				.MinimumLevel.Debug()
				.WriteTo.Console()
				.WriteTo.File(logPath, rollingInterval: RollingInterval.Day, outputTemplate: outputTemplate)
				.CreateLogger();
            
			CreateHostBuilder(args).Build().Run();
		}

		private static IHostBuilder CreateHostBuilder(string[] args) {
			return Host.CreateDefaultBuilder(args)
				.ConfigureLogging((hostingContext, logging) => {
					logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
					logging.AddConsole();
					logging.AddDebug();
				})
				.ConfigureWebHostDefaults(webBuilder => {
					webBuilder.UseStartup<Startup>();
					webBuilder.UseUrls("http://*","https://*");
				})
				// Initialize our dream screen emulator
				.ConfigureServices(services => {
					services.AddSignalR();
					services.AddSingleton<ControlService>();
					services.AddHostedService<DreamService>();
					services.AddHostedService<StatService>();
					services.AddHostedService<DiscoveryService>();
					services.AddHostedService<ColorService>();
				});
		}
	}
}