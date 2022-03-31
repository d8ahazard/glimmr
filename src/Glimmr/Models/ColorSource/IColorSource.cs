#region

using System;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.FrameUtils;
using Microsoft.Extensions.Hosting;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource;

public abstract class ColorSource : BackgroundService {
	public abstract bool SourceActive { get; }

	public abstract FrameBuilder? Builder { get; set; }
	public abstract FrameSplitter Splitter { get; set; }
	protected Task? RunTask;
	public abstract Task Start(CancellationToken ct);

	public void Stop() {
		if (RunTask == null) {
			return;
		}

		try {
			RunTask.Dispose();
		} catch (Exception e) {
			Log.Debug("Exception killing run task: " + e.Message);
		}
	}

	public abstract void RefreshSystem();
}