﻿#region

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
		private readonly ColorService _cs;
		private AudioStream? _as;
		private Task? _aTask;
		private bool _doSave;
		private SystemData _systemData;
		private VideoStream? _vs;
		private Task? _vTask;
		private Color[] _colors;
		private Color[] _sectors;

		public AudioVideoStream(ColorService cs) {
			_cs = cs;
			_systemData = DataUtil.GetSystemData();
			_colors = ColorUtil.EmptyColors(_systemData.LedCount);
			_sectors = ColorUtil.EmptyColors(_systemData.SectorCount);
			_cs.ControlService.RefreshSystemEvent += RefreshSystem;
			_cs.FrameSaveEvent += TriggerSave;
			RefreshSystem();
		}

		public Task ToggleStream(CancellationToken ct) {
			Log.Debug("Starting av stream service...");
			var aS = _cs.GetStream(DeviceMode.Audio.ToString());
			var vS = _cs.GetStream(DeviceMode.Video.ToString());
			if (aS != null) {
				_as = (AudioStream) aS;
			}

			if (vS != null) {
				_vs = (VideoStream) vS;
			}

			if (_vs != null && _as != null) {
				_vTask = _vs.ToggleStream(ct);
				_vs.SendColors = false;
				_vs.StreamSplitter.DoSend = false;
				_aTask = _as.ToggleStream(ct);
				_as.SendColors = false;
				_as.StreamSplitter.DoSend = false;
			} else {
				Log.Warning("Unable to acquire audio or video stream.");
				return Task.CompletedTask;
			}

			return ExecuteAsync(ct);
		}

		public void RefreshSystem() {
			_systemData = DataUtil.GetSystemData();
		}
		
		public Color[] GetColors() {
			return _colors;
		}

		public Color[] GetSectors() {
			return _sectors;
		}


		public bool SourceActive => _vs != null && _vs.StreamSplitter.SourceActive;

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

					var vCols = _vs.StreamSplitter.GetColors();
					var vSecs = _vs.StreamSplitter.GetSectors();
					var aCols = _as.StreamSplitter.GetColors();
					var aSecs = _as.StreamSplitter.GetSectors();
					if (vCols.Length == 0 || vCols.Length != aCols.Length || vSecs.Length == 0 ||
					    vSecs.Length != aSecs.Length) {
						Log.Debug(
							$"AV Splitter is still warming up...");
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

					_colors = oCols;
					_sectors = oSecs;

					if (_doSave) {
						_doSave = false;
						_vs.StreamSplitter.MergeFrame(oCols, oSecs);
					}

					await Task.Delay(16, CancellationToken.None);
				}

				if (_aTask != null && !_aTask.IsCompleted) {
					_aTask.Dispose();
				}

				if (_vTask != null && !_vTask.IsCompleted) {
					_vTask.Dispose();
				}

				Log.Debug("AV stream service stopped.");
			}, CancellationToken.None);
		}
	}
}