#region

using System;
using System.Runtime.InteropServices;
using CoreGraphics;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using ObjCRuntime;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.Video.Stream.Screen;

public class OSxScreenshot {
	private readonly int _displayId;
	private IntPtr _handle;

	public OSxScreenshot() {
		Log.Debug("Made our screenshot deal.");
		try {
			_displayId = CGDisplay.MainDisplayID;
		} catch (Exception e) {
			Log.Warning("Broke it: " + e.Message + " at " + e.StackTrace);
		}
	}

	[DllImport(Constants.CoreGraphicsLibrary)]
	private static extern IntPtr CGDisplayCreateImage(int displayId);

	[DllImport(Constants.CoreGraphicsLibrary)]
	private static extern void CFRelease(IntPtr handle);

	public Image<Bgr, byte>? Grab() {
		_handle = IntPtr.Zero;

		try {
			_handle = CGDisplayCreateImage(_displayId);
			var cimg = new CGImage(_handle);
			var img = new Mat((int)cimg.Height, (int)cimg.Width, DepthType.Cv8U, 4);
			var output = new Mat((int)cimg.Height, (int)cimg.Width, DepthType.Cv8U, 3);
			var csRef = cimg.ColorSpace;
			var contextRef = new CGBitmapContext(img.DataPointer, cimg.Width, cimg.Height, 8,
				img.Step, csRef, CGImageAlphaInfo.PremultipliedLast);
			contextRef.DrawImage(new CGRect(0, 0, cimg.Width, cimg.Height), cimg);
			CvInvoke.CvtColor(img, output, ColorConversion.Rgba2Bgr);
			var ti = output.ToImage<Bgr, byte>();
			output.Dispose();
			img.Dispose();
			cimg.Dispose();
			if (_handle != IntPtr.Zero) {
				CFRelease(_handle);
			}

			var sized = ti.Resize(640, 480, Inter.Nearest);
			ti.Dispose();
			return sized;
		} catch (Exception e) {
			Log.Warning("Matt is exceptional: " + e);
		}

		return null;
	} // End Sub CreateImage 
} // End Class OSxScreenshot 