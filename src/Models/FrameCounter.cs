using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Services;

namespace Glimmr.Models {
	public class FrameCounter : IDisposable {
		private readonly Stopwatch _stopwatch;
		private Dictionary<string, int> _ticks;

		public FrameCounter(ColorService cs) {
			_ticks = new Dictionary<string, int>();
			_stopwatch = new Stopwatch();
			cs.ControlService.SetModeEvent += Mode;
		}

		public void Dispose() {
			_stopwatch.Stop();
		}

		private Task Mode(object arg1, DynamicEventArgs arg2) {
			var newMode = (DeviceMode) arg2.P1;
			if (newMode != DeviceMode.Off) {
				_stopwatch.Restart();
				_ticks = new Dictionary<string, int>();
			} else {
				_stopwatch.Stop();
			}

			return Task.CompletedTask;
		}

		public void Tick(string id) {
			if (_ticks.Keys.Contains(id)) {
				_ticks[id]++;
			} else {
				_ticks[id] = 1;
			}
		}

		public int Rate(string id) {
			if (!_ticks.Keys.Contains(id)) {
				return 0;
			}

			var avg = _ticks[id] / (_stopwatch.ElapsedMilliseconds / 1000);
			return (int) avg;
		}

		public Dictionary<string, long> Rates() {
			var time = _stopwatch.ElapsedMilliseconds / 1000;
			var output = new Dictionary<string, long>();
			foreach (var (key, value) in _ticks) {
				output[key] = time != 0 ? value / time : 0;
			}

			return output;
		}
	}
}