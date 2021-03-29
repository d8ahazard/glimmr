using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using Glimmr.Services;

namespace Glimmr.Models.ColorSource.DreamScreen {
	public class DreamScreenStream : ColorSource, IColorSource {
		public DreamScreenStream(ColorService cs, ControlService cos, CancellationToken ct) : base(cs, cos, ct) {
		}

		public void StartStream(CancellationToken ct) {
			throw new System.NotImplementedException();
		}

		public void StopStream() {
			throw new System.NotImplementedException();
		}

		public void Refresh() {
			throw new System.NotImplementedException();
		}

		public List<Color> Colors { get; }
		public List<Color> Sectors { get; }
	}
}