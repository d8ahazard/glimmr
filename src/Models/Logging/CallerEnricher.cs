using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
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
					var mName = method.DeclaringType.Name;
					var name = method.Name;
					if (method.Name == "MoveNext") {
						name = mName;
						mName = method.DeclaringType.DeclaringType.Name;
						if (mName == "") mName = method.DeclaringType.Name;
					}

					if (name.Contains("<") && name.Contains(">")) {
						var pFrom = name.IndexOf("<", StringComparison.Ordinal) + 1;
						var pTo = name.LastIndexOf(">", StringComparison.Ordinal);
						name = name.Substring(pFrom, pTo - pFrom);
					}

					if (mName.Contains("<") && mName.Contains(">")) {
						var pFrom = mName.IndexOf("<", StringComparison.Ordinal) + 1;
						var pTo = mName.LastIndexOf(">", StringComparison.Ordinal);
						mName = mName.Substring(pFrom, pTo - pFrom);
					}
					var caller =
						$"[{mName}][{name}]";
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