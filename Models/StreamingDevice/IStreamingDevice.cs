using System.Collections.Generic;
using System.Drawing;
using System.Threading;

namespace Glimmr.Models.StreamingDevice {
	public interface IStreamingDevice {
		public bool Streaming { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }
		public bool Enable { get; set; }
        
		public StreamingData Data { get; set; }
        
		public abstract void StartStream(CancellationToken ct);

		public abstract void StopStream();

		public abstract void SetColor(List<Color> colors, double fadeTime);

		public bool IsEnabled() {
			return Enable;
		}

		public abstract void ReloadData();

		public abstract void Dispose();
	}
}