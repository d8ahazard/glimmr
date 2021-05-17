using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DreamScreenNet;
using DreamScreenNet.Enum;
using Glimmr.Models.ColorTarget.DreamScreen;
using Glimmr.Models.Util;
using Glimmr.Services;
using Microsoft.Extensions.Hosting;
using Serilog;
using DeviceMode = Glimmr.Enums.DeviceMode;
using IPAddress = System.Net.IPAddress;

namespace Glimmr.Models.ColorSource.DreamScreen {
	public class DreamScreenStream : BackgroundService, IColorSource {
		private readonly ColorService _cs;
		private readonly DreamScreenClient _client;
		private IPAddress _targetDreamScreen;
		private const int TargetGroup = 20;
		private bool _enable;

		public DreamScreenStream(ColorService colorService) {
			_cs = colorService;
			_cs.AddStream(DeviceMode.DreamScreen, this);
			_client = _cs.ControlService.GetAgent("DreamAgent");
			_client.CommandReceived += ProcessCommand;
			_cs.ControlService.RefreshSystemEvent += RefreshSd;
			RefreshSd();
		}

		private void RefreshSd() {
			var systemData = DataUtil.GetSystemData();
			var dsIp = systemData.DsIp;
			if (string.IsNullOrEmpty(dsIp)) {
				var devs = DataUtil.GetDevices();
				foreach (var dd in from dev in devs where dev.Tag == "DreamScreen" select (DreamScreenData) dev into dd where dd.DeviceTag.Contains("DreamScreen") select dd) {
					Log.Debug("No target set, setting to " + dd.IpAddress);
					systemData.DsIp = dd.IpAddress;
					DataUtil.SetSystemData(systemData);
					dsIp = dd.IpAddress;
					break;
				}
			}
			
			if (!string.IsNullOrEmpty(dsIp)) {
				_targetDreamScreen = IPAddress.Parse(dsIp);
			}
		}

		public void ToggleStream(bool enable) {
			_enable = enable;
			if (_enable) {
				Log.Debug("Target is " + _targetDreamScreen);
				_client.StartSubscribing(_targetDreamScreen);
			} else {
				_client.StopSubscribing();
			}
		}

		private void ProcessCommand(object? sender, DreamScreenClient.MessageEventArgs e) {
			if (e.Response.Group == TargetGroup) {
				Log.Debug("Incoming command from target DS: " + e.Response.Type);
				switch (e.Response.Type) {
					case MessageType.Mode:
						var mode = e.Response.Payload.GetUint8();
						Log.Debug("New mode is " + mode);
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
				Log.Debug($"{TargetGroup} command from " + e.Response.Target);
			}
		}

		public void Refresh(SystemData systemData) {
			var dsIp = systemData.DsIp;
			
			if (!string.IsNullOrEmpty(dsIp)) {
				_targetDreamScreen = IPAddress.Parse(dsIp);
			}
		}

		public bool SourceActive { get; set; }

		protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
			_client.ColorsReceived += UpdateColors;
			while (!stoppingToken.IsCancellationRequested) {
				await Task.Delay(1, stoppingToken);
			}
			_client.StopSubscribing();
		}

		private void UpdateColors(object? sender, DreamScreenClient.DeviceColorEventArgs e) {
			var colors = e.Colors;
			var ledColors = ColorUtil.SectorsToleds(colors.ToList(),5,3);
			_cs.SendColors(ledColors, colors.ToList(),0);
		}
	}
}