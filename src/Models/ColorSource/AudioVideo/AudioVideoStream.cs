#region

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

#endregion

namespace Glimmr.Models.ColorSource.AudioVideo {
	public class AudioVideoStream : BackgroundService, IColorSource {
		private List<Color> _colors;
		private List<Color> _sectors;
		private readonly ColorService _cs;
		private VideoStream? _vs;
		private AudioStream? _as;

		private bool _enable;
		private SystemData _systemData;

		public AudioVideoStream(ColorService cs) {
			_cs = cs;
			_colors = new List<Color>();
			_sectors = new List<Color>();
			_systemData = DataUtil.GetSystemData();
		}

		public Task ToggleStream(CancellationToken ct) {
			Log.Debug("Starting av stream service...");
			var aS = _cs.GetStream(DeviceMode.Audio.ToString());
			var vS = _cs.GetStream(DeviceMode.Video.ToString());
			if (aS != null) _as = (AudioStream) aS; 
			if (vS != null) _vs = (VideoStream) vS;
			if (_vs != null && _as != null) {
				_vs.ToggleStream(ct);
				_vs.SendColors = false;
				_as.ToggleStream(ct);
				_as.SendColors = false;
			} else {
				Log.Warning("Unable to acquire audio or video stream.");
				return Task.CompletedTask;
			}
			return ExecuteAsync(ct);
		}


		public void Refresh(SystemData systemData) {
			_systemData = systemData;
			_colors = ColorUtil.EmptyList(_systemData.LedCount);
			_sectors = ColorUtil.EmptyList(28);
		}

		public bool SourceActive { get; set; }

		protected override Task ExecuteAsync(CancellationToken ct) {
			
			return Task.Run(async () => {
				while (!ct.IsCancellationRequested) {
					if (!_enable || _vs == null || _as == null) {
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

					_colors = oCols;
					_sectors = oSecs;
					//_cs.SendColors(this, new DynamicEventArgs(oCols, oSecs)).ConfigureAwait(true);
					_cs.SendColors(_colors, _sectors, 0);
					await Task.Delay(1, CancellationToken.None);
				}

				Log.Debug("AV stream service stopped.");
			}, CancellationToken.None);
		}
	}
}