using System.Collections.Generic;
using System.Threading;
using Glimmr.Models.ColorSource.Audio;
using Glimmr.Models.ColorSource.Video;
using System.Drawing;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Serilog;

namespace Glimmr.Models.ColorSource.AudioVideo {
	public class AudioVideoStream : IColorSource {
		public bool Streaming { get; set; }
		public List<Color> Colors { get; private set; }
		public List<Color> Sectors { get; private set; }
		private readonly ColorService _cs;
		private readonly VideoStream _vs;
		private readonly AudioStream _as;
		private SystemData _systemData;
		public AudioVideoStream(ColorService cs, AudioStream aus, VideoStream vs) {
			_cs = cs;
			_as = aus;
			_vs = vs;
		}

		public async void Initialize(CancellationToken ct) {
			Refresh();
			while (!ct.IsCancellationRequested) {
				var vCols = _vs.Colors;
				var vSecs = _vs.Sectors;
				var aCols = _as.Colors;
				var aSecs = _as.Sectors;
				if (vCols.Count == 0 || vCols.Count != aCols.Count || vSecs.Count == 0 || vSecs.Count != aSecs.Count) {
					Log.Debug($"AV Splitter is still warming up {aCols.Count}, {aSecs.Count}, {vCols.Count}, {vSecs.Count}");
					continue;
				}
				var oCols = new List<Color>();
				var oSecs = new List<Color>();
				for (var i = 0; i < vCols.Count; i++) {
					var aCol = aCols[i];
					var vCol = vCols[i];
					oCols.Add(ColorUtil.SetBrightness(vCol, aCol.GetBrightness()));
				}
				
				for (var i = 0; i < vSecs.Count; i++) {
					var aCol = aSecs[i];
					var vCol = vSecs[i];
					oSecs.Add(ColorUtil.SetBrightness(vCol, aCol.GetBrightness()));
				}

				Colors = oCols;
				Sectors = oSecs;
				_cs.SendColors(oCols, oSecs);
				await Task.Delay(16, CancellationToken.None);
			}
		}
		
		public void ToggleSend(bool enable = false) {
			Streaming = enable;
		}


		
		public void Refresh() {
			_systemData = DataUtil.GetObject<SystemData>("SystemData");
			Colors = ColorUtil.EmptyList(_systemData.LedCount);
			Sectors = ColorUtil.EmptyList(28);
		}
		
		
	}
}