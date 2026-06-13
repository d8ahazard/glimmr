#region

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models.Helper;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;

#endregion

namespace Glimmr.Models.Frame;

public class FrameCounter : IDisposable {
	/// <summary>
	///     Current per-source frame rates. Decays to zero when the source stops
	///     ticking (input lost, capture device restarted) instead of freezing at
	///     the last computed value.
	/// </summary>
	public ConcurrentDictionary<string, int> Rates {
		get {
			if (DateTime.UtcNow - _lastTickUtc > TimeSpan.FromSeconds(3)) {
				return new ConcurrentDictionary<string, int> { ["source"] = 0 };
			}

			return _rates;
		}
		private set => _rates = value;
	}

	private readonly Stopwatch _stopwatch;
	private DateTime _lastTickUtc = DateTime.MinValue;
	private DeviceMode _mode;
	private ConcurrentDictionary<string, int> _rates;
	private ConcurrentDictionary<string, int> _ticks;

	public FrameCounter(ColorService cs) {
		_ticks = new ConcurrentDictionary<string, int> { ["source"] = 0 };
		_rates = _ticks;
		_stopwatch = new Stopwatch();
		cs.ControlService.SetModeEvent += Mode;
		cs.ControlService.RefreshSystemEvent += RefreshSystem;
		var sd = DataUtil.GetSystemData();
		_mode = sd.DeviceMode;
		if (sd.AutoDisabled) {
			_mode = DeviceMode.Off;
		}
	}

	public void Dispose() {
		_stopwatch.Stop();
		GC.SuppressFinalize(this);
	}

	private void RefreshSystem() {
		var sd = DataUtil.GetSystemData();

		_mode = sd.DeviceMode;
		if (_mode != DeviceMode.Off) {
			_stopwatch.Restart();
		} else {
			_stopwatch.Stop();
		}

		_ticks = new ConcurrentDictionary<string, int> { ["source"] = 0 };
		Rates = _ticks;
	}


	private Task Mode(object arg1, DynamicEventArgs arg2) {
		var newMode = (DeviceMode)arg2.Arg0;
		_mode = newMode;
		if (newMode != DeviceMode.Off) {
			_stopwatch.Restart();
		} else {
			_stopwatch.Stop();
		}

		_ticks = new ConcurrentDictionary<string, int> { ["source"] = 0 };
		Rates = _ticks;
		return Task.CompletedTask;
	}

	public void Tick(string id) {
		// Make sure watch is running
		if (_mode == DeviceMode.Off) {
			_ticks = new ConcurrentDictionary<string, int> { ["source"] = 0 };
			return;
		}

		_lastTickUtc = DateTime.UtcNow;

		if (!_stopwatch.IsRunning) {
			_stopwatch.Start();
		}

		if (_stopwatch.Elapsed >= TimeSpan.FromMilliseconds(1000)) {
			_stopwatch.Restart();
			Rates = _ticks;
			_ticks = new ConcurrentDictionary<string, int> { ["source"] = 0 };
		}

		try {
			if (_ticks.ContainsKey(id)) {
				_ticks[id]++;
			} else {
				_ticks[id] = 0;
			}
		} catch (Exception e) {
			Log.Debug("Exception ticking: " + e.Message);
			_ticks = new ConcurrentDictionary<string, int> { ["source"] = 0 };
		}
	}

	public void Reset() {
		_ticks = new ConcurrentDictionary<string, int> { ["source"] = 0 };
		Rates = _ticks;
	}
}