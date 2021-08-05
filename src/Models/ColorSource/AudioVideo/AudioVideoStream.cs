#region

using System.Drawing;
using System.Linq;
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
		private Color[] _colors;
		private Color[] _sectors;
		private readonly ColorService _cs;
		private VideoStream? _vs;
		private AudioStream? _as;
		private Task _vTask;
		private Task _aTask;
		private bool _enable;
		private bool _doSave;
		private SystemData _systemData;

		public AudioVideoStream(ColorService cs) {
			_cs = cs;
			_cs.ControlService.RefreshSystemEvent += RefreshSystem;
			_cs.FrameSaveEvent += TriggerSave;
			RefreshSystem();
		}

		private void TriggerSave() {
			_doSave = true;
		}

		public Task ToggleStream(CancellationToken ct) {
			Log.Debug("Starting av stream service...");
			var aS = _cs.GetStream(DeviceMode.Audio.ToString());
			var vS = _cs.GetStream(DeviceMode.Video.ToString());
			if (aS != null) _as = (AudioStream) aS; 
			if (vS != null) _vs = (VideoStream) vS;
			if (_vs != null && _as != null) {
				_vTask = _vs.ToggleStream(ct);
				_vs.SendColors = false;
				_vs.StreamSplitter.DoSend = false;
				_aTask = _as.ToggleStream(ct);
				_as.SendColors = false;
				_as.Splitter.DoSend = false;
			} else {
				Log.Warning("Unable to acquire audio or video stream.");
				return Task.CompletedTask;
			}
			return ExecuteAsync(ct);
		}

		private void RefreshSystem() {
			Refresh(DataUtil.GetSystemData());
		}

		public void Refresh(SystemData systemData) {
			_systemData = systemData;
			_colors = ColorUtil.EmptyColors(new Color[_systemData.LedCount]);
			_sectors = ColorUtil.EmptyColors(new Color[_systemData.SectorCount]);
		}

		public bool SourceActive { get; set; }

		protected override Task ExecuteAsync(CancellationToken ct) {
			
			return Task.Run(async () => {
				while (!ct.IsCancellationRequested) {
					if (_vs == null || _as == null) {
						if (_vs == null) Log.Warning("Video stream is null.");
						if (_as == null) Log.Warning("Audio stream is null.");
						continue;
					}
					var vCols = _vs.StreamSplitter.GetColors();
					var vSecs = _vs.StreamSplitter.GetSectors();
					var aCols = _as.Splitter.GetColors();
					var aSecs = _as.Splitter.GetSectors();
					if (vCols.Count == 0 || vCols.Count != aCols.Count || vSecs.Count == 0 ||
					    vSecs.Count != aSecs.Count) {
						Log.Debug(
							$"AV Splitter is still warming up {aCols.Count}, {aSecs.Count}, {vCols.Count}, {vSecs.Count}");
						continue;
					}

					var oCols = new Color[_systemData.LedCount];
					var oSecs = new Color[_systemData.SectorCount];
					for (var i = 0; i < vCols.Count; i++) {
						var ab = aCols[i].GetBrightness();
						var vCol = vCols[i];
						oCols[i] = ColorUtil.SetBrightness(vCol, ab);
					}

					for (var i = 0; i < vSecs.Count; i++) {
						var ab = aSecs[i].GetBrightness();
						var vCol = vSecs[i];
						oSecs[i] = ColorUtil.SetBrightness(vCol, ab);
					}

					_colors = oCols;
					_sectors = oSecs;
					_cs.SendColors(_colors.ToList(), _sectors.ToList(), 0);

					if (_doSave) {
						_doSave = false;
						await _vs.StreamSplitter.MergeFrame(_colors, _sectors).ConfigureAwait(false);
					}
					await Task.Delay(16, CancellationToken.None);
				}	
				Log.Debug("AV stream service stopped.");
			}, CancellationToken.None);
		}
	}
}