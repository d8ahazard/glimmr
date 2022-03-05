#region

using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models.ColorSource.Audio;
using Glimmr.Models.ColorSource.Video;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.AudioVideo;

public class AudioVideoStream : ColorSource {
	public override bool SourceActive => _vs != null && _vs.Splitter.SourceActive;
	
	private readonly ColorService _cs;
	public sealed override FrameSplitter Splitter { get; set; } 
	private AudioStream? _as;
	private Task? _aTask;
	private bool _doSave;
	private SystemData _systemData;
	private VideoStream? _vs;
	private Task? _vTask;
	public sealed override FrameBuilder? Builder { get; set; }

	public AudioVideoStream(ColorService cs) {
		_cs = cs;
		var vS = (ColorSource?) _cs.GetStream(DeviceMode.Video.ToString());
		Splitter = vS != null ? vS.Splitter : new FrameSplitter(cs);
		_systemData = DataUtil.GetSystemData();
		_cs.ControlService.RefreshSystemEvent += RefreshSystem;
		_cs.FrameSaveEvent += TriggerSave;
	}

	public override Task Start(CancellationToken ct) {
		RefreshSystem();
		var aS = _cs.GetStream(DeviceMode.Audio.ToString());
		var vS = _cs.GetStream(DeviceMode.Video.ToString());
		if (aS != null) {
			_as = (AudioStream)aS;
		}

		if (vS != null) {
			_vs = (VideoStream)vS;
		}

		if (_vs != null && _as != null) {
			Log.Debug("Starting video stream...");
			_vTask = _vs.Start(ct);
			_vs.SendColors = false;
			Log.Debug("Starting audio stream...");
			_aTask = _as.Start(ct);
			_as.SendColors = false;
		} else {
			Log.Warning("Unable to acquire audio or video stream.");
			return Task.CompletedTask;
		}
		Log.Debug("Starting main av loop...");
		RunTask = ExecuteAsync(ct);
		return Task.CompletedTask;
	}

	public override void RefreshSystem() {
		_systemData = DataUtil.GetSystemData();
	}

	private void TriggerSave() {
		_doSave = true;
	}

	protected override Task ExecuteAsync(CancellationToken ct) {
		return Task.Run(async () => {
			while (!ct.IsCancellationRequested) {
				if (_vs == null || _as == null) {
					if (_vs == null) {
						Log.Warning("Video stream is null.");
					}

					if (_as == null) {
						Log.Warning("Audio stream is null.");
					}

					continue;
				}
				var vCols = _vs.Splitter.GetColors();
				var vSecs = _vs.Splitter.GetSectors();
				var aCols = _as.Splitter.GetColors();
				var aSecs = _as.Splitter.GetSectors();
				if (vCols.Length == 0 || vCols.Length != aCols.Length || vSecs.Length == 0 ||
				    vSecs.Length != aSecs.Length) {
					continue;
				}
				var oCols = new Color[_systemData.LedCount];
				var oSecs = new Color[_systemData.SectorCount];
				for (var i = 0; i < vCols.Length; i++) {
					var ab = aCols[i].GetBrightness();
					var vCol = vCols[i];
					oCols[i] = ColorUtil.SetBrightness(vCol, ab);
				}

				for (var i = 0; i < vSecs.Length; i++) {
					var ab = aSecs[i].GetBrightness();
					var vCol = vSecs[i];
					oSecs[i] = ColorUtil.SetBrightness(vCol, ab);
				}
				await _cs.SendColors(oCols, oSecs);

				if (_doSave && _cs.ControlService.SendPreview) {
					_doSave = false;
					Log.Debug("Merge...");
					_vs.Splitter.MergeFrame(oCols, oSecs);
				}
				await Task.Delay(16, CancellationToken.None);
			}

			if (_vs != null) {
				_vs.SendColors = true;
			}

			if (_as != null) {
				_as.SendColors = true;
			}

			if (_aTask is { IsCompleted: false }) {
				_aTask.Dispose();
			}

			if (_vTask is { IsCompleted: false }) {
				_vTask.Dispose();
			}

			Log.Debug("AV stream service stopped.");
		}, CancellationToken.None);
	}
}