using HueDream.Models.DreamScreen;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;

namespace HueDream {
    public static class Program {
        public static void Main(string[] args) {
            CreateHostBuilder(args).Build().Run();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) {
            return Host.CreateDefaultBuilder(args)
                .ConfigureLogging((hostingContext, logging) =>
                {
                    logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                    logging.AddConsole();
                    logging.AddDebug();
                })    
                .ConfigureWebHostDefaults(webBuilder => { 
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseUrls("http://*:5699");


                })
                // Initialize our dream screen emulator
                .ConfigureServices(services => { services.AddHostedService<DreamClient>();});
        }
    }
}