using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Emgu.CV;
using Glimmr.Enums;
using Glimmr.Services;
using Serilog;

namespace Glimmr.Models {
	public class FrameCounter : IDisposable {
		public Dictionary<string, int> Ticks;
		private Stopwatch _stopwatch;
		
		public FrameCounter(ColorService cs) {
			Ticks = new Dictionary<string, int>();
			_stopwatch = new Stopwatch();
			cs.ControlService.SetModeEvent += Mode;
		}

		private Task Mode(object arg1, DynamicEventArgs arg2) {
			var newMode = (DeviceMode) arg2.P1;
			if (newMode != DeviceMode.Off) {
				_stopwatch.Restart();
				Ticks = new Dictionary<string, int>();
			} else {
				_stopwatch.Stop();
			}
			return Task.CompletedTask;
		}

		public void Tick(string id) {
			if (Ticks.Keys.Contains(id)) {
				Ticks[id]++;
			} else {
				Ticks[id] = 1;
			}
		}

		public int Rate(string id) {
			if (!Ticks.Keys.Contains(id)) {
				return 0;
			}

			var avg = Ticks[id] / (_stopwatch.ElapsedMilliseconds / 1000);
			return (int) avg;
		}

		public void Dispose() {
			_stopwatch.Stop();
		}

		public Dictionary<string, long> Rates() {
			var time = _stopwatch.ElapsedMilliseconds / 1000;
			var output = new Dictionary<string, long>();
			foreach (var (key, value) in Ticks) {
				output[key] = time != 0 ? value / time : 0;
			}

			return output;
		}
	}
}