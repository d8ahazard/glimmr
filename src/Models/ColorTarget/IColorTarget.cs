using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Services;
using Newtonsoft.Json;

namespace Glimmr.Models.ColorTarget {
	public interface IColorTarget {
		public bool Streaming { get; set; }
		public bool Testing { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }
		public bool Enable { get; set; }
        
		public IColorTargetData Data { get; set; }
        
		public Task StartStream(CancellationToken ct);

		public Task StopStream();

		public void SetColor(List<Color> colors, List<Color> sectors, int fadeTime);

		public Task FlashColor(Color color);

		public bool IsEnabled() {
			return Enable;
		}

		public Task ReloadData();

		public void Dispose();
		
		[JsonProperty]
		public DateTime LastSeen => DateTime.Now;
	}

	public abstract class ColorTarget {
		public ColorService ColorService { get; }
		public ControlService ControlService { get; }
		protected ColorTarget(ColorService cs) {
			ColorService = cs;
			ControlService = cs.ControlService;
		}

		protected ColorTarget() {
			
		}
	}
}