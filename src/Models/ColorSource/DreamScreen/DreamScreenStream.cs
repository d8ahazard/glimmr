#region

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DreamScreenNet;
using DreamScreenNet.Devices;
using DreamScreenNet.Enum;
using Glimmr.Models.ColorTarget.DreamScreen;
using Glimmr.Models.Util;
using Glimmr.Services;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Serilog;
using DeviceMode = Glimmr.Enums.DeviceMode;

#endregion

namespace Glimmr.Models.ColorSource.DreamScreen {
	public class DreamScreenStream : BackgroundService, IColorSource {
		private const int TargetGroup = 20;
		private readonly DreamScreenClient? _client;
		private readonly ColorService _cs;
		private DreamDevice? _dDev;
		public bool Enable { get; set; }
		private IPAddress? _targetDreamScreen;
		private int _hCount;
		private int _vCount;
		private int _ledCount;
		private int _sectorCount;
		private Dictionary<int, int> _ledMap;
		private Dictionary<int, int> _sectorMap;

		public DreamScreenStream(ColorService colorService) {
			_cs = colorService;
			var client = _cs.ControlService.GetAgent("DreamAgent");
			if (client != null) {
				_client = client;
				_client.CommandReceived += ProcessCommand;
				_client.SubscriptionRequested += SubRequested;
			}
			_cs.ControlService.RefreshSystemEvent += RefreshSd;
			RefreshSd();
		}

		public Task ToggleStream(CancellationToken ct) {
			if (_client == null || _dDev == null || _targetDreamScreen == null) return Task.CompletedTask;
			Log.Debug("Starting DS stream, Target is " + _targetDreamScreen + " group is " + TargetGroup);
			_client.SetMode(_dDev, DreamScreenNet.Enum.DeviceMode.Off);
			_client.SetMode(_dDev, DreamScreenNet.Enum.DeviceMode.Video);
			_client.StartSubscribing(_targetDreamScreen);
			Log.Debug("DS Stream should be started...");
			Log.Debug("Starting DS stream service...");
			_client.ColorsReceived += UpdateColors;

			return ExecuteAsync(ct);
		}

		public void Refresh(SystemData systemData) {
			var dsIp = systemData.DsIp;

			if (!string.IsNullOrEmpty(dsIp)) {
				_targetDreamScreen = IPAddress.Parse(dsIp);
			}
		}

		public bool SourceActive { get; set; }

		private void SubRequested(object? sender, DreamScreenClient.DeviceSubscriptionEventArgs e) {
				if (Equals(e.Target, _targetDreamScreen)) {
					Log.Debug("Incoming sub request from target!" + e.Target);
				} else {
					Log.Debug("incoming sub request, but it's not from our target: " + e.Target);
				}
		}

		private void RefreshSd() {
			var systemData = DataUtil.GetSystemData();
			var dsIp = systemData.DsIp;
			_hCount = systemData.HSectors;
			_vCount = systemData.VSectors;
			_sectorCount = systemData.SectorCount;
			_ledCount = systemData.LedCount;
			_sectorMap = ColorUtil.MapSectors(12, new[] {3, 5});
			_ledMap = ColorUtil.MapLedsFromSectors(12, 5, 3);
			// If our DS IP is null, pick one.
			if (string.IsNullOrEmpty(dsIp)) {
				var devs = DataUtil.GetDevices();
				foreach (var dd in from dev in devs
					where dev.Tag == "DreamScreen"
					select (DreamScreenData) dev
					into dd
					where dd.DeviceTag.Contains("DreamScreen")
					select dd) {
					Log.Debug("No target set, setting to " + dd.IpAddress);
					systemData.DsIp = dd.IpAddress;
					DataUtil.SetSystemData(systemData);
					dsIp = dd.IpAddress;
					break;
				}
			}

			if (!string.IsNullOrEmpty(dsIp)) {
				var dsData = DataUtil.GetDevice<DreamScreenData>(dsIp);
				if (dsData != null) {
					_dDev = new DreamDevice {DeviceGroup = dsData.GroupNumber};
					_dDev.Type = dsData.DeviceTag switch {
						"DreamScreenHd" => DeviceType.DreamScreenHd,
						"DreamScreen4K" => DeviceType.DreamScreen4K,
						"DreamScreenSolo" => DeviceType.DreamScreenSolo,
						_ => _dDev.Type
					};
					_dDev.IpAddress = IPAddress.Parse(dsData.IpAddress);
				}

				_targetDreamScreen = IPAddress.Parse(dsIp);
			}
		}

		private void ProcessCommand(object? sender, DreamScreenClient.MessageEventArgs e) {
			Log.Debug("Incoming command from DS: " + e.Response.Type);
			if (_dDev == null) return;
			if (e.Response.Group == TargetGroup || e.Response.Group == _dDev.DeviceGroup) {
				switch (e.Response.Type) {
					case MessageType.Mode:
						var mode = int.Parse(e.Response.Payload.ToString());
						Log.Debug("Toggle mode: " + mode);
						_cs.ControlService.SetMode(mode).ConfigureAwait(false);
						break;
					case MessageType.Brightness:
						break;
					case MessageType.AmbientColor:
						break;
					case MessageType.AmbientScene:
						break;
					case MessageType.AmbientModeType:
						_cs.ControlService.SetMode(3).ConfigureAwait(false);
						break;
				}
			} else {
				Log.Debug($"{TargetGroup} doesn't match {e.Response.Group} or {_dDev.DeviceGroup} command from " +
				          e.Response.Target);
			}
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
			while (!stoppingToken.IsCancellationRequested) {
				await Task.Delay(1, stoppingToken);
			}

			Log.Debug("DS stream service stopped.");
			_client.StopSubscribing();
		}

		private void UpdateColors(object? sender, DreamScreenClient.DeviceColorEventArgs e) {
			var colors = e.Colors;
			if (colors.Length != _sectorCount) {
				var cols = new Color[_sectorCount];
				foreach (var (key, value) in _sectorMap) {
					cols[key] = colors[value];
				}
				colors = cols;
			}
			var leds = new Color[_ledCount];
			foreach (var (key, value) in _ledMap) {
				leds[key] = colors[value];
			}
			_cs.Counter.Tick("Dreamscreen");
			_cs.SendColors(leds.ToList(), colors.ToList(), 0);
		}
	}
}