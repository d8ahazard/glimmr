using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models.ColorSource.Audio;
using Glimmr.Models.ColorSource.Video;
using Glimmr.Models.Util;
using Glimmr.Services;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Glimmr.Models.ColorSource.AudioVideo {
	public class AudioVideoStream : BackgroundService, IColorSource {
		public List<Color> Colors { get; private set; }
		public List<Color> Sectors { get; private set; }
		private readonly AudioStream _as;
		private readonly ColorService _cs;
		private readonly VideoStream _vs;
		private bool _enable;
		private SystemData _systemData;

		public AudioVideoStream(ColorService cs) {
			_cs = cs;
			_cs.AddStream(DeviceMode.AudioVideo, this);
			_as = (AudioStream) _cs.GetStream(DeviceMode.Audio);
			_vs = (VideoStream) _cs.GetStream(DeviceMode.Video);
		}


		public void ToggleStream(bool enable = false) {
			_enable = enable;
			if (!enable) {
				return;
			}

			_vs.ToggleStream(true);
			_vs.SendColors = false;
			_as.ToggleStream(true);
			_as.SendColors = false;
		}


		public void Refresh(SystemData systemData) {
			_systemData = systemData;
			Colors = ColorUtil.EmptyList(_systemData.LedCount);
			Sectors = ColorUtil.EmptyList(28);
		}

		public bool SourceActive { get; set; }

		protected override Task ExecuteAsync(CancellationToken ct) {
			Log.Debug("Starting av stream service...");
			return Task.Run(async () => {
				while (!ct.IsCancellationRequested) {
					if (!_enable) {
						continue;
					}

					var vCols = _vs.Colors;
					var vSecs = _vs.Sectors;
					var aCols = _as.Colors;
					var aSecs = _as.Sectors;
					if (vCols.Count == 0 || vCols.Count != aCols.Count || vSecs.Count == 0 ||
					    vSecs.Count != aSecs.Count) {
						Log.Debug(
							$"AV Splitter is still warming up {aCols.Count}, {aSecs.Count}, {vCols.Count}, {vSecs.Count}");
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
					//_cs.SendColors(this, new DynamicEventArgs(oCols, oSecs)).ConfigureAwait(true);
					_cs.SendColors(Colors, Sectors, 0);
					await Task.Delay(1, CancellationToken.None);
				}

				Log.Debug("AV stream service stopped.");
			}, CancellationToken.None);
		}
	}
}