using System.Diagnostics;
using System.Linq;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace Glimmr.Models.Logging {
	class CallerEnricher : ILogEventEnricher {
		public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory) {
			var skip = 3;
			while (true) {
				var stack = new StackFrame(skip);
				if (!stack.HasMethod()) {
					logEvent.AddPropertyIfAbsent(new LogEventProperty("Caller", new ScalarValue("<unknown method>")));
					return;
				}

				var method = stack.GetMethod();
				if (method.DeclaringType.Assembly != typeof(Log).Assembly) {
					var caller =
						$"{method.DeclaringType.FullName}.{method.Name}({string.Join(", ", method.GetParameters().Select(pi => pi.ParameterType.FullName))})";
					logEvent.AddPropertyIfAbsent(new LogEventProperty("Caller", new ScalarValue(caller)));
					return;
				}

				skip++;
			}
		}
	}

	static class LoggerCallerEnrichmentConfiguration {
		public static LoggerConfiguration WithCaller(this LoggerEnrichmentConfiguration enrichmentConfiguration) {
			return enrichmentConfiguration.With<CallerEnricher>();
		}
	}
}