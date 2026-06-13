#region

using System;
using System.IO;
using System.Reflection;
using Glimmr.Hubs;
using Glimmr.Models.Helper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;

#endregion

namespace Glimmr;

public class Startup {
	// This method gets called by the runtime. Use this method to add services to the container.
	public static void ConfigureServices(IServiceCollection services) {
		var val = "0.0.0.0";
		var assembly = Assembly.GetEntryAssembly();
		if (assembly != null) {
			var attrib = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
			if (attrib != null) {
				val = attrib.InformationalVersion;
			}
		}

		var Version = val.Split("-")[0];
		services.AddControllers()
			.AddJsonOptions(options => { options.JsonSerializerOptions.PropertyNamingPolicy = null; })
			.AddNewtonsoftJson();
		services.AddCors(options => {
			options.AddPolicy("LanClients", policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
		});
		services.AddSwaggerGen(c => {
			var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
			var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
			c.IncludeXmlComments(xmlPath);
			c.UseOneOfForPolymorphism();
			c.EnableAnnotations(true, true);
			c.SchemaFilter<DescribeEnumMembers>(xmlPath);
			c.SwaggerDoc(Version, new OpenApiInfo {
				Version = Version,
				Title = "Glimmr Web API",
				Description = "A simple example ASP.NET Core Web API",
				Contact = new OpenApiContact {
					Name = "d8ahazard",
					Email = "donate.to.digitalhigh@gmail.com",
					Url = new Uri("https://facebook.com/GlimmrTV")
				},
				License = new OpenApiLicense {
					Name = "GPL3.0",
					Url = new Uri("https://github.com/d8ahazard/glimmr/blob/master/COPYING")
				}
			});
		});
		var settings = new JsonSerializerSettings { ContractResolver = new SignalRContractResolver() };
		var serializer = JsonSerializer.Create(settings);
		services.AddSingleton(serializer);
	}

	// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
	public static void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
		var val = "0.0.0.0";
		var assembly = Assembly.GetEntryAssembly();
		if (assembly != null) {
			var attrib = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
			if (attrib != null) {
				val = attrib.InformationalVersion;
			}
		}

		var Version = val.Split("-")[0];

		app.UseDeveloperExceptionPage();
		// NOTE: HTTPS redirect is intentionally disabled. Glimmr is a LAN appliance served over
		// HTTP, and the emulated Hue Bridge must answer on both HTTP (v1 /api) and HTTPS (CLIP v2)
		// without forcing redirects. The HTTPS/443 endpoint is added in Program.cs for Hue Sync.
		var staticFiles = new StaticFileOptions {
			OnPrepareResponse = ctx => {
				// The SPA entry point must always revalidate so UI updates appear on a
				// plain refresh; hashed assets are immutable and can cache forever.
				if (ctx.File.Name == "index.html") {
					ctx.Context.Response.Headers.CacheControl = "no-cache";
				} else if (ctx.Context.Request.Path.StartsWithSegments("/app/assets")) {
					ctx.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
				}
			}
		};
		app.UseStaticFiles(staticFiles);
		app.UseSwagger();
		app.UseSwaggerUI(c => { c.SwaggerEndpoint($"/swagger/{Version}/swagger.json", $"My API {Version}"); });
		app.UseWebSockets();
		app.UseRouting();
		app.UseCors("LanClients");
		app.UseAuthorization();

		app.UseEndpoints(endpoints => {
			endpoints.MapControllers();
			endpoints.MapHub<SocketServer>("/socket");
			// The web UI is a static SPA built from src/Glimmr.Web into wwwroot/app.
			endpoints.MapFallbackToFile("app/index.html", staticFiles);
		});
		app.Use(async (context, next) => {
			var unused = context.RequestServices
				.GetRequiredService<IHubContext<SocketServer>>();
			await next.Invoke();
		});
	}
}