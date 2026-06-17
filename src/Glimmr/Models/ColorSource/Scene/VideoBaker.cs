#region

using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.Scene;

/// <summary>
///     Bakes an uploaded video into a <see cref="SceneV2" /> frame sequence. Decodes with
///     Emgu.CV's <see cref="VideoCapture" />, trims, resamples to the target size/fps (area
///     averaging), and writes a gzipped RGB blob plus the scene manifest. The blob is streamed to
///     disk so even multi-minute clips don't have to be held in memory.
///     NOTE: server-side decode needs a working video backend (ffmpeg) on the host. On platforms
///     where that's unavailable, the UI can fall back to extracting frames in the browser.
/// </summary>
public class VideoBaker {
	// Absolute safety cap so a long upload can't fill the disk / RAM on a Pi.
	private const int MaxFrames = 18000;

	public class Options {
		public string Name { get; set; } = "Video Scene";
		public int Width { get; set; } = 32;
		public int Height { get; set; } = 48;
		public double Fps { get; set; } = 20;
		public double TrimStart { get; set; }
		public double TrimEnd { get; set; }

		/// <summary>
		///     Playback speed multiplier. 1 = real time, &lt;1 stretches the clip out (a 20s
		///     timelapse at 0.5 plays over 40s), &gt;1 compresses it. Implemented by duplicating
		///     or skipping source frames at bake time.
		/// </summary>
		public double Speed { get; set; } = 1;

		/// <summary>
		///     How the source is scaled to the target grid: "stretch" (fill, ignore aspect),
		///     "crop" (cover: fill preserving aspect, center-crop overflow), or "fit" (contain:
		///     fit preserving aspect, letterbox the remainder).
		/// </summary>
		public string Fit { get; set; } = "crop";

		public bool Loop { get; set; } = true;
		public bool Reverse { get; set; }
		public bool Mirror { get; set; }
	}

	private readonly SceneStore _store;

	public VideoBaker(SceneStore store) {
		_store = store;
	}

	public SceneV2 Bake(string inputPath, Options opts) {
		var w = Math.Clamp(opts.Width, 1, 128);
		var h = Math.Clamp(opts.Height, 1, 128);
		var targetFps = Math.Clamp(opts.Fps, 1, 60);
		var speed = Math.Clamp(opts.Speed <= 0 ? 1 : opts.Speed, 0.1, 10);
		var fit = (opts.Fit ?? "crop").Trim().ToLowerInvariant();
		var trimStart = Math.Max(0, opts.TrimStart);
		var trimEnd = opts.TrimEnd > trimStart ? opts.TrimEnd : double.MaxValue;

		using var capture = new VideoCapture(inputPath);
		var srcFps = capture.Get(CapProp.Fps);
		if (srcFps <= 1 || double.IsNaN(srcFps) || double.IsInfinity(srcFps)) {
			srcFps = 30;
		}

		var id = Guid.NewGuid().ToString("N");
		var framesFile = id + ".bin.gz";
		var blobPath = Path.Join(_store.UserDir, framesFile);

		var frame = new Mat();
		var small = new Mat(h, w, DepthType.Cv8U, 3);
		var scratch = new Mat();
		var frameBytes = new byte[w * h * 3];
		var srcIndex = 0;
		var emitted = 0;

		try {
			using var fs = File.Create(blobPath);
			using var gz = new GZipStream(fs, CompressionLevel.Optimal);
			while (emitted < MaxFrames) {
				if (!capture.Read(frame) || frame.IsEmpty) {
					break;
				}

				var ts = srcIndex / srcFps;
				srcIndex++;
				if (ts < trimStart) {
					continue;
				}

				if (ts > trimEnd) {
					break;
				}

				var rel = ts - trimStart;
				// Output frame j maps to source offset (j / targetFps) * speed. Emit every
				// output frame now due: speed < 1 duplicates this source frame (stretch),
				// speed > 1 skips ahead (compress).
				if (emitted / targetFps * speed > rel + 1e-9) {
					continue;
				}

				ScaleFrame(frame, small, scratch, w, h, fit);
				using (var image = small.ToImage<Bgr, byte>()) {
					var bi = 0;
					for (var y = 0; y < h; y++) {
						for (var x = 0; x < w; x++) {
							var px = image[y, x];
							frameBytes[bi++] = (byte)px.Red;
							frameBytes[bi++] = (byte)px.Green;
							frameBytes[bi++] = (byte)px.Blue;
						}
					}
				}

				while (emitted < MaxFrames && emitted / targetFps * speed <= rel + 1e-9) {
					gz.Write(frameBytes, 0, frameBytes.Length);
					emitted++;
				}
			}
		} catch (Exception e) {
			Log.Warning($"VideoBaker failure: {e.Message}");
			frame.Dispose();
			small.Dispose();
			scratch.Dispose();
			TryDelete(blobPath);
			throw new InvalidOperationException(
				"Unable to decode video on the server (a video codec/ffmpeg backend may be missing). " +
				e.Message);
		}

		frame.Dispose();
		small.Dispose();
		scratch.Dispose();

		if (emitted == 0) {
			TryDelete(blobPath);
			throw new InvalidOperationException("No frames could be decoded from the uploaded video.");
		}

		var scene = new SceneV2 {
			Id = id,
			Name = string.IsNullOrWhiteSpace(opts.Name) ? "Video Scene" : opts.Name.Trim(),
			Kind = "video",
			Width = w,
			Height = h,
			Fps = targetFps,
			Loop = opts.Loop,
			Reverse = opts.Reverse,
			Mirror = opts.Mirror,
			FramesFile = framesFile,
			FrameCount = emitted
		};
		_store.Save(scene);
		Log.Information($"Baked video scene '{scene.Name}': {emitted} frames at {w}x{h}@{targetFps}fps.");
		return scene;
	}

	/// <summary>
	///     Scale <paramref name="src" /> into the <paramref name="w" />x<paramref name="h" />
	///     <paramref name="dst" /> using the requested fit mode. "stretch" fills and distorts;
	///     "crop" covers (preserve aspect, center-crop); "fit" contains (preserve aspect, black bars).
	/// </summary>
	private static void ScaleFrame(Mat src, Mat dst, Mat scratch, int w, int h, string fit) {
		var sw = src.Width;
		var sh = src.Height;
		if (sw <= 0 || sh <= 0) {
			return;
		}

		switch (fit) {
			case "stretch":
				CvInvoke.Resize(src, dst, new Size(w, h), 0, 0, Inter.Area);
				return;
			case "fit": {
				var scale = Math.Min((double)w / sw, (double)h / sh);
				var nw = Math.Max(1, (int)Math.Round(sw * scale));
				var nh = Math.Max(1, (int)Math.Round(sh * scale));
				CvInvoke.Resize(src, scratch, new Size(nw, nh), 0, 0, Inter.Area);
				dst.SetTo(new MCvScalar(0, 0, 0));
				var x = (w - nw) / 2;
				var y = (h - nh) / 2;
				using (var sub = new Mat(dst, new Rectangle(x, y, nw, nh))) {
					scratch.CopyTo(sub);
				}

				return;
			}
			default: { // "crop" (cover)
				var scale = Math.Max((double)w / sw, (double)h / sh);
				var nw = Math.Max(w, (int)Math.Ceiling(sw * scale));
				var nh = Math.Max(h, (int)Math.Ceiling(sh * scale));
				CvInvoke.Resize(src, scratch, new Size(nw, nh), 0, 0, Inter.Area);
				var x = (nw - w) / 2;
				var y = (nh - h) / 2;
				using (var sub = new Mat(scratch, new Rectangle(x, y, w, h))) {
					sub.CopyTo(dst);
				}

				return;
			}
		}
	}

	private static void TryDelete(string path) {
		try {
			if (File.Exists(path)) {
				File.Delete(path);
			}
		} catch (Exception) {
			// ignored
		}
	}
}
