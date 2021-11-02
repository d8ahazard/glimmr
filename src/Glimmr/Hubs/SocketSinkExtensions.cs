#region

using Serilog;
using Serilog.Configuration;

#endregion

namespace Glimmr.Hubs {
	public static class SocketSinkExtensions {
		public static LoggerConfiguration SocketSink(this LoggerSinkConfiguration loggerConfiguration) {
			return loggerConfiguration.Sink(new SocketSink());
		}
	}
}