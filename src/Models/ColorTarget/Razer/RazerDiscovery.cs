using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Colore;
using Colore.Data;
using Glimmr.Services;
using Serilog;

namespace Glimmr.Models.ColorTarget.Razer {
	public class RazerDiscovery : ColorDiscovery, IColorDiscovery {
		private IChroma _chroma;
		private ControlService _controlService;
		private bool _hasChroma;
		
		public RazerDiscovery(ColorService colorService) : base(colorService) {
			DeviceTag = "Razer";
			_controlService = colorService.ControlService;
			_chroma = _controlService.GetAgent("RazerAgent");
		}
		

		public async Task Discover(CancellationToken ct) {
			Log.Debug("Razer: Discovery started...");
			await CheckDevices(ct);
			Log.Debug("Razer: Discovery complete.");
		}


		private async Task CheckDevices(CancellationToken ct) {
			var done = false;
			while (!ct.IsCancellationRequested && !done) {
				try {
					if (!File.Exists(Environment.GetEnvironmentVariable("ProgramW6432") +
					                 @"\Razer Chroma SDK\bin\RzChromaSDK64.dll")) {
						Log.Debug(
							Environment.Is64BitOperatingSystem
								? "The Razer SDK (RzChromaSDK64.dll) Could not be found on this computer. Uninstall any previous versions of Razer SDK & Synapse and then reinstall Razer Synapse."
								: "The Razer SDK (RzChromaSDK.dll) Could not be found on this computer. Uninstall any previous versions of Razer SDK & Synapse and then reinstall Razer Synapse.");
						_hasChroma = false;
						return;
					}

					
					//_chroma = await ColoreProvider.CreateNativeAsync();

					Log.Debug(@"Razer SDK Loaded (" + _chroma.Version + ")");

				} catch (Exception e) {
					Log.Warning("Razer issue: " + e.Message);
					if (e.InnerException != null) Log.Debug(e.InnerException.ToString());
					return;
				}

				var devices = new Dictionary<Guid, DeviceInfo>();
				// Enumerate every razer GUID and check to see if device is attached
				var props = typeof(Devices).GetFields(BindingFlags.Public |
				                                      BindingFlags.Static |
				                                      BindingFlags.FlattenHierarchy)
					.ToList();

				Log.Debug("Enumerating " + props.Count + " devices.");
				foreach (var prop in props) {
					var guid = prop.GetValue(null)?.ToString();
					if (guid == null) {
						continue;
					}

					var guidVal = Guid.Parse(guid);
					try {
						var valid = Devices.IsValidId(guidVal);
						if (valid) {
							var info = await _chroma.QueryAsync(guidVal);
							if (info != null) {
								Log.Debug("Device discovered!: " + info.Description);
								devices[guidVal] = info;
							} else {
								Log.Debug("Device is null...");
							}
						}
					} catch (Exception) {
						// ignored						
					}
				}

				foreach (var info in devices.Select(dev => dev.Value).Where(info => info.Type != DeviceType.Invalid)) {
					await _controlService.AddDevice(new RazerData(info));
				}
				done = true;
			}
		}



		public sealed override string DeviceTag { get; set; }
	}
}