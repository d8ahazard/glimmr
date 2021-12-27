#region

using Glimmr.Services;
using Serilog.Core;
using Serilog.Events;

#endregion

namespace Glimmr.Hubs; 

public class SocketSink : ILogEventSink {
	private ControlService? _cs;

	public void Emit(LogEvent logEvent) {
		if (_cs == null) {
			_cs = ControlService.GetInstance();
		}

		_cs?.SendLogLine(logEvent).ConfigureAwait(false);
	}
}