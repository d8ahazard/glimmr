#region

using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.Scene;

/// <summary>
///     Replays a baked video frame sequence. The gzipped blob is decompressed once into memory;
///     each frame is Width*Height*3 RGB bytes (row-major, top-left). Supports forward looping and
///     ping-pong (when <see cref="SceneV2.Reverse" /> is set).
/// </summary>
public class FramePlayer : ISceneProvider {
	private readonly byte[] _data;
	private readonly int _frameBytes;
	private readonly int _frameCount;
	private readonly bool _loop;
	private readonly bool _pingPong;
	private int _index;
	private int _step = 1;

	public FramePlayer(SceneV2 scene, string blobPath) {
		_w = Math.Max(1, scene.Width);
		_h = Math.Max(1, scene.Height);
		Fps = scene.Fps < 1 ? 30 : scene.Fps;
		_loop = scene.Loop;
		_pingPong = scene.Reverse;
		_frameBytes = _w * _h * 3;

		byte[] data;
		try {
			using var fs = File.OpenRead(blobPath);
			using var gz = new GZipStream(fs, CompressionMode.Decompress);
			using var ms = new MemoryStream();
			gz.CopyTo(ms);
			data = ms.ToArray();
		} catch (Exception e) {
			Log.Warning($"FramePlayer: failed to load blob '{blobPath}': {e.Message}");
			data = Array.Empty<byte>();
		}

		_data = data;
		_frameCount = _frameBytes > 0 ? _data.Length / _frameBytes : 0;
	}

	private readonly int _w;
	private readonly int _h;
	public int Width => _w;
	public int Height => _h;
	public double Fps { get; }

	public void NextFrame(Color[] grid) {
		if (_frameCount == 0) {
			Array.Clear(grid, 0, grid.Length);
			return;
		}

		var frame = Math.Clamp(_index, 0, _frameCount - 1);
		var offset = frame * _frameBytes;
		var count = Math.Min(grid.Length, _frameBytes / 3);
		for (var i = 0; i < count; i++) {
			var j = offset + i * 3;
			grid[i] = Color.FromArgb(_data[j], _data[j + 1], _data[j + 2]);
		}

		Advance();
	}

	private void Advance() {
		_index += _step;
		if (_index >= _frameCount) {
			if (_pingPong) {
				_step = -1;
				_index = Math.Max(0, _frameCount - 2);
			} else if (_loop) {
				_index = 0;
			} else {
				_index = _frameCount - 1; // hold last frame
			}
		} else if (_index < 0) {
			if (_loop) {
				_step = 1;
				_index = Math.Min(_frameCount - 1, 1);
			} else {
				_index = 0; // hold first frame
			}
		}
	}
}
