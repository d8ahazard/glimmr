#region

using System.Collections.Generic;
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
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.DreamScreen {
	public class DreamScreenStream : BackgroundService, IColorSource {
		private const int TargetGroup = 20;
		private readonly DreamScreenClient? _client;
		private readonly ColorService _cs;
		private DreamDevice? _dDev;
		private IPAddress? _targetDreamScreen;
		private FrameBuilder _builder;
		private FrameSplitter _splitter;

		public DreamScreenStream(ColorService colorService) {
			_cs = colorService;
			
			var client = _cs.ControlService.GetAgent("DreamAgent");
			if (client != null) {
				_client = client;
				_client.CommandReceived += ProcessCommand;
			}

			var rect = new[]{3,3,5,5};
			_builder = new FrameBuilder(rect, true);
			_splitter = colorService.Splitter;
			_cs.ControlService.RefreshSystemEvent += RefreshSd;
			RefreshSd();
		}

		public Task ToggleStream(CancellationToken ct) {
			if (_client == null || _dDev == null || _targetDreamScreen == null) return Task.CompletedTask;
			Log.Debug("Starting DS stream, Target is " + _targetDreamScreen + " group is " + TargetGroup);
			_client.StartSubscribing(_targetDreamScreen);
			_client.SetMode(_dDev, DeviceMode.Off);
			_client.SetMode(_dDev, DeviceMode.Video);
			Log.Debug("Starting DS stream service...");
			return ExecuteAsync(ct);
		}

		public void Refresh(SystemData systemData) {
			var dsIp = systemData.DsIp;

			if (!string.IsNullOrEmpty(dsIp)) {
				_targetDreamScreen = IPAddress.Parse(dsIp);
			}
		}

		public bool SourceActive { get; set; }

	

		private void RefreshSd() {
			var systemData = DataUtil.GetSystemData();
			var dsIp = systemData.DsIp;
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
						if (mode == 1) mode = 5; // Video = streaming
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
			if (_client != null) _client.ColorsReceived += UpdateColors;
			_splitter.DoSend = true;
			while (!stoppingToken.IsCancellationRequested) {
				await Task.Delay(1, stoppingToken);
			}

			if (_client != null) {
				Log.Information("Stopping subscription...");
				_client.StopSubscribing();
				_client.ColorsReceived -= UpdateColors;
			}
			_splitter.DoSend = false;
			Log.Debug("DS stream service stopped.");
		}

		private void UpdateColors(object? sender, DreamScreenClient.DeviceColorEventArgs e) {
			var colors = e.Colors;
			var frame = _builder.Build(colors);
			_splitter.Update(frame);
			frame.Dispose();
			SourceActive = !_splitter.NoImage;
			_cs.Counter.Tick("Dreamscreen");
		}
	}
}