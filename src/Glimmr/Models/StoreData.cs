using System.Collections.Generic;
using System.Reflection;
using Glimmr.Models.ColorSource.Ambient;
using Glimmr.Models.ColorSource.Audio;
using Glimmr.Models.Util;
using Newtonsoft.Json;
using Serilog;

namespace Glimmr.Models {
	/// <summary>
	/// A JSON representation of the database.
	/// </summary>
	public class StoreData {
		/// <summary>
		/// Main SystemData Object.
		/// </summary>
		[JsonProperty]
		public SystemData SystemData {get;set;}
		/// <summary>
		/// List of devices and their settings.
		/// </summary>
		[JsonProperty]
		public dynamic[] Devices {get;set;}
		
		/// <summary>
		/// List of available audio devices.
		/// </summary>
		[JsonProperty]
		public AudioData[] DevAudio {get;set;}
		
		/// <summary>
		/// List of available USB devices.
		/// </summary>
		[JsonProperty]
		public Dictionary<int, string> DevUsb {get;set;}
		
		/// <summary>
		/// List of detected ambient scenes.
		/// </summary>
		[JsonProperty]
		public AmbientScene[] AmbientScenes {get;set;}
		
		/// <summary>
		/// List of detected audio scenes.
		/// </summary>
		[JsonProperty]
		public AudioScene[] AudioScenes {get;set;}
		
		/// <summary>
		/// Version string based on current build + github revison.
		/// </summary>
		[JsonProperty]
		public string Version {get;set;}

		public StoreData() {
			SystemData = DataUtil.GetSystemData();
			DevAudio = DataUtil.GetCollection<AudioData>("Dev_Audio").ToArray();
			Devices = DataUtil.GetDevices().ToArray();
			DevUsb = SystemUtil.ListUsb();
			var jl1 = new JsonLoader("ambientScenes");
			var jl2 = new JsonLoader("audioScenes");
			AmbientScenes = jl1.LoadDynamic<AmbientScene>().ToArray();
			Log.Debug("Ambient scenes: " + JsonConvert.SerializeObject(AmbientScenes));
			AudioScenes = jl2.LoadDynamic<AudioScene>().ToArray();
			Version = "0.0.0.0";
			var assembly = Assembly.GetEntryAssembly();
			if (assembly != null) {
				var attrib = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
				if (attrib != null) {
					Version = attrib.InformationalVersion;
				}
			}
		}
	}
}