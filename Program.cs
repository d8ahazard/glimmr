using HueDream.DreamScreen;
using HueDream.Models.DreamScreen;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HueDream {
    public static class Program {
        public static void Main(string[] args) {
            CreateHostBuilder(args).Build().Run();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) {
            return Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder => {
                    webBuilder.UseStartup<Startup>();
                })
                .ConfigureServices(services => {
                    services.AddHostedService<DreamClient>();
                });
        }
    }
}