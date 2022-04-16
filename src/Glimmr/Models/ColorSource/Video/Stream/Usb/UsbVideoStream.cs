#region

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Glimmr.Models.Frame;
using Glimmr.Models.Util;
using Serilog;
using static Glimmr.Models.Constant.GlimmrConstants;

#endregion

namespace Glimmr.Models.ColorSource.Video.Stream.Usb;

public class UsbVideoStream : IVideoStream, IDisposable {
	private bool _disposed;
	private int _inputStream;
	private FrameSplitter? _splitter;
	private VideoCapture? _video;
	private readonly Mat? _frame;
	
	public UsbVideoStream() {
		_frame = new();
	}

	public async Task Start(FrameSplitter splitter, CancellationToken ct) {
		Log.Debug("Starting USB Stream...");
		
		_splitter = splitter;
		await Refresh();
		if (_video == null) {
			return;
		}

		await Task.Run( async () => {
			while (!ct.IsCancellationRequested) {
				await GrabFrame().ConfigureAwait(false);
			}
		}, CancellationToken.None).ConfigureAwait(false);
		//
		// _video.ImageGrabbed += SetFrame;
		// _video.Start();
		Log.Debug("USB Stream started.");
	}

	private async Task GrabFrame() {
		if (_video == null) return;
		if (_splitter == null) return;
		if (_video.Ptr == IntPtr.Zero) return;
		if (_video.Grab()) {
			if (_video.Retrieve(_frame)) await _splitter.Update(_frame?.Clone()).ConfigureAwait(true);
		}
	}


	public Task Stop() {
		Dispose();
		return Task.CompletedTask;
	}

	private Task Refresh() {
		var sd = DataUtil.GetSystemData();
		var inputStream = sd.UsbSelection;
		if (inputStream != _inputStream || _video == null) {
			_inputStream = inputStream;
			SetVideo();
		}

		if (CheckVideo()) {
			return Task.CompletedTask;
		}

		SetVideo();
		if (!CheckVideo()) {
			Log.Warning("Still unable to set video.");
		}


		return Task.CompletedTask;
	}

	private void SetVideo() {
		_video?.Dispose();
		var props = new Tuple<CapProp, int>[] {
			new(CapProp.FrameWidth, ScaleWidth),
			new(CapProp.FrameHeight, ScaleHeight),
			new(CapProp.Fps, 60),
			new(CapProp.FourCC, VideoWriter.Fourcc('M', 'J', 'P', 'G')),
			new(CapProp.Buffersize, 3)
		};
		var api = OperatingSystem.IsWindows() ? VideoCapture.API.DShow : VideoCapture.API.V4L2;
		if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
			api = VideoCapture.API.Any;
		}

		_video = new VideoCapture(_inputStream, api);

		foreach (var (prop, val) in props) {
			_video.Set(prop, val);
		}
	}

	private bool CheckVideo() {
		if (_video == null) {
			Log.Warning("Unable to set video stream.");
			return false;
		}

		var d5 = VideoWriter.Fourcc('M', 'J', 'P', 'G');

		try {
			_video.Set(CapProp.Fps, 60);
			_video.Set(CapProp.FourCC, d5);
		} catch (Exception e) {
			Log.Debug("Exception setting video prop: " + e.Message);
		}

		var fourCc = (int)_video.Get(CapProp.FourCC);
		var fps = (int)_video.Get(CapProp.Fps);

		Log.Debug($"Video created, fps and 4cc are {fps} and {fourCc} versus 60 and {d5}.");
		return true;
	}

	private void DisposeVideo() {
		Log.Debug("Disposing video.");
		try {
			if (_video != null) {
				_video.Dispose();
				_video = null;
				_frame?.Dispose();
			} else {
				Log.Debug("Video is null");
			}
		} catch (Exception e) {
			Log.Warning("Exception: " + e.Message);
		}
	}

	protected virtual void Dispose(bool disposing) {
		if (!disposing) {
			return;
		}

		DisposeVideo();
	}
	
	public void Dispose() {
		if (_disposed) {
			return;
		}

		_disposed = true;
		GC.SuppressFinalize(this);
		Dispose(true);
	}

}