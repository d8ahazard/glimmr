using System.Collections.Generic;
using System.Drawing;
using System.Threading;

namespace HueDream.Models.StreamingDevice {
    public interface IStreamingDevice {
        public bool Streaming { get; set; }
        public int Brightness { get; set; }
        public string Id { get; set; }
        public string IpAddress { get; set; }
        public void StartStream(CancellationToken ct) {
            while (!ct.IsCancellationRequested) {
                Streaming = true;
            }
            StopStream();
        }

        public void StopStream() {
            Streaming = false;
        }

        public void SetColor(List<Color> colors, double fadeTime) {
        }
       
     
        public void ReloadData() {
            
        }

        public void Dispose() {
        }
    }
}