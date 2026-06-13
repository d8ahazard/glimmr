#region

using System;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Frame;
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
		var task = RunTask;
		RunTask = null;
		if (task == null) {
			return;
		}

		try {
			// Tasks can only be disposed once completed; cancellation is handled by the
			// caller's token, so just release our reference (and dispose if finished).
			if (task.IsCompleted) {
				task.Dispose();
			}
		} catch (Exception e) {
			Log.Debug("Exception killing run task: " + e.Message);
		}
	}

	public abstract void RefreshSystem();
}