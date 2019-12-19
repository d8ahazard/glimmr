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
        private DreamClient Dc;
        
        public Startup(IConfiguration configuration) {
            Console.WriteLine(@"Startup called...");
            Dc = new DreamClient();
            Console.WriteLine(@"DC Started.");
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void Dispose() {
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