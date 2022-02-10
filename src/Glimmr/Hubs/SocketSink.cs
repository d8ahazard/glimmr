#region

using System;
using Glimmr.Services;
using Serilog.Core;
using Serilog.Events;

#endregion

namespace Glimmr.Hubs;

public class SocketSink : ILogEventSink {
	private ControlService? _cs;

	public void Emit(LogEvent logEvent) {
		_cs ??= ControlService.GetInstance();
		try {
			_cs.SendLogLine(logEvent).ConfigureAwait(false);
		} catch (Exception) {
			// Ignored
		}
	}
}