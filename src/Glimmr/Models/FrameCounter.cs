﻿#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Services;

#endregion

namespace Glimmr.Models {
	public class FrameCounter : IDisposable {
		public Dictionary<string, int> Rates { get; private set; }
		private readonly Stopwatch _stopwatch;
		private Dictionary<string, int> _ticks;

		public FrameCounter(ColorService cs) {
			_ticks = new Dictionary<string, int> {["source"] = 0};
			Rates = _ticks;
			_stopwatch = new Stopwatch();
			cs.ControlService.SetModeEvent += Mode;
		}

		public void Dispose() {
			_stopwatch.Stop();
		}

		private Task Mode(object arg1, DynamicEventArgs arg2) {
			var newMode = (DeviceMode) arg2.Arg0;
			if (newMode != DeviceMode.Off) {
				_stopwatch.Restart();
			} else {
				_stopwatch.Stop();
			}

			_ticks = new Dictionary<string, int> {["source"] = 0};
			Rates = _ticks;
			return Task.CompletedTask;
		}

		public void Tick(string id) {
			// Make sure watch is running
			if (!_stopwatch.IsRunning) {
				_stopwatch.Start();
			}

			// Clear our cache every minute so we don't wind up with massive stored values over time
			if (_stopwatch.Elapsed >= TimeSpan.FromSeconds(1)) {
				_stopwatch.Restart();
				Rates = _ticks;
				_ticks = new Dictionary<string, int> {["source"] = 0};
			}

			if (_ticks.Keys.Contains(id)) {
				_ticks[id]++;
			} else {
				_ticks[id] = 0;
			}
		}

		public void Reset() {
			_ticks = new Dictionary<string, int> {["source"] = 0};
			Rates = _ticks;
		}
	}
}