using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.Structure;
using Pranas;

namespace HueDream.Models.CaptureSource.HDMI {
    public class HdmiVideoStream : IVideoStream {
        public Mat Frame { get; set; }

        Mat IVideoStream.Frame {
            get => Frame;
            set => Frame = value;
        }

        public Task Start(CancellationToken ct) {
            while (!ct.IsCancellationRequested) {
                
            }

            return Task.CompletedTask;
        }

        
    }
}