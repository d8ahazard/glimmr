using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace HueDream {
    public static class Program {
        public static void Main(string[] args) {
            CreateHostBuilder(args).Build().Run();
        }

        private static IWebHostBuilder CreateHostBuilder(string[] args) {
            return WebHost.CreateDefaultBuilder(args).UseStartup<Startup>();
        }
    }
}