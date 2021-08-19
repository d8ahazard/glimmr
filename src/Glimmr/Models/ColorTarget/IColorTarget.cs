#region

using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Services;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget {
	public interface IColorTarget {
		public bool Enable { get; set; }
		public bool Streaming { get; set; }
		public bool Testing { set; }
		[JsonProperty] public DateTime LastSeen => DateTime.Now;

		public IColorTargetData Data { get; set; }
		public string Id { get; }

		public Task StartStream(CancellationToken ct);

		public Task StopStream();
		
		public Task FlashColor(Color color);


		public Task ReloadData();

		public void Dispose();
	}

	public abstract class ColorTarget {
		public ColorService? ColorService { get; init; }

		protected ColorTarget(ColorService cs) {
			ColorService = cs;
		}

		protected ColorTarget() {
		}
	}
}