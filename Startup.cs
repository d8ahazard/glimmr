using System;
using System.Threading;
using HueDream.Models.DreamScreen;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HueDream {
    public class Startup : IDisposable {
        public DreamClient Dc;
        private CancellationTokenSource ct;

        public Startup(IConfiguration configuration) {
            Console.WriteLine(@"Startup called...");
            Configuration = configuration;
            Dc = new DreamClient();
            ct = new CancellationTokenSource();
            Dc.StartAsync(ct.Token);
        }

        public IConfiguration Configuration { get; }

        public void Dispose() {
            ct.Cancel();
            Dc.Dispose();
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public static void ConfigureServices(IServiceCollection services) {
            services.AddControllersWithViews();
            services.AddControllers()
                .AddNewtonsoftJson();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();
            else
                app.UseExceptionHandler("/Home/Error");

            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();
            
            app.UseEndpoints(endpoints => {
                endpoints.MapControllerRoute(
                    "default",
                    "{controller=Home}/{action=Index}/{id?}");
            });
        }

    }
}