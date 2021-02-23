using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace Glimmr.Models.ColorTarget.Corsair {
	public class CorsairDevice : IStreamingDevice {
		public bool Streaming { get; set; }
		public bool Testing { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }
		public bool Enable { get; set; }
		public StreamingData Data { get; set; }
		public Task StartStream(CancellationToken ct) {
			throw new System.NotImplementedException();
		}

		public Task StopStream() {
			throw new System.NotImplementedException();
		}

		public void SetColor(List<Color> colors, List<Color> sectors, int fadeTime) {
			throw new System.NotImplementedException();
		}

		public Task FlashColor(Color color) {
			throw new System.NotImplementedException();
		}

		public Task ReloadData() {
			throw new System.NotImplementedException();
		}

		public void Dispose() {
			throw new System.NotImplementedException();
		}
	}
}