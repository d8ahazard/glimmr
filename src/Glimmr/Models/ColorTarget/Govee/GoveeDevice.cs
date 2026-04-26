#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Govee;

public class GoveeDevice : ColorTarget, IColorTarget, IDisposable {
	private static readonly HttpClient HttpClient = new();
	private readonly UdpClient _udpClient;

	private GoveeData _data;
	private bool _disposed;
	private IPEndPoint? _ep;
	private int _brightness;
	private int _ledCount;
	private float _multiplier;
	private int _offset;
	private StripMode _stripMode;
	private int _targetSector;

	private const int LanControlPort = 4003;
	private const string CloudControlUrl =
		"https://openapi.api.govee.com/router/api/v1/device/control";

	public GoveeDevice(GoveeData gd, ColorService cs) : base(cs) {
		cs.ControlService.RefreshSystemEvent += RefreshSystem;
		_udpClient = cs.ControlService.UdpClient;
		_data = gd ?? throw new ArgumentException("Invalid Govee data.");
		Id = _data.Id;
		_brightness = _data.Brightness;
		_multiplier = _data.LedMultiplier;
		ReloadData();
		ColorService.ColorSendEventAsync += SetColors;
	}

	public bool Enable { get; set; }
	public bool Streaming { get; set; }
	public string Id { get; }

	IColorTargetData IColorTarget.Data {
		get => _data;
		set => _data = (GoveeData)value;
	}

	public async Task StartStream(CancellationToken ct) {
		if (Streaming || !Enable) return;

		Log.Debug("{Tag}::Starting stream: {Id}...", _data.Tag, _data.Id);
		_targetSector = _data.TargetSector;

		if (_data.LanEnabled && !string.IsNullOrEmpty(_data.IpAddress)) {
			_ep = IpUtil.Parse(_data.IpAddress, LanControlPort);
		}

		await SetBrightness(_brightness);
		await SetPower(true);
		await FlashColor(Color.Black);
		Streaming = true;
		Log.Debug("{Tag}::Stream started: {Id}.", _data.Tag, _data.Id);
	}

	public async Task StopStream() {
		if (!Streaming) return;

		Log.Debug("{Tag}::Stopping stream: {Id}...", _data.Tag, _data.Id);
		Streaming = false;
		await FlashColor(Color.Black);
		await SetPower(false);
		Log.Debug("{Tag}::Stream stopped: {Id}.", _data.Tag, _data.Id);
	}

	public async Task FlashColor(Color color) {
		if (_ep != null) {
			await SendLanColor(color.R, color.G, color.B);
		} else if (!string.IsNullOrEmpty(_data.GoveeApiKey)) {
			await SendCloudColor(color.R, color.G, color.B);
		}
	}

	public async Task SetColors(IReadOnlyList<Color> ledColors, IReadOnlyList<Color> sectorColors) {
		if (!Streaming || !Enable) return;

		if (_data.SupportsSegments && !string.IsNullOrEmpty(_data.GoveeApiKey) && _data.SegmentCount > 0) {
			await SendSegmentColors(sectorColors);
			return;
		}

		Color target;
		switch (_stripMode) {
			case StripMode.Single when _targetSector > sectorColors.Count || _targetSector == -1:
				return;
			case StripMode.Single:
				target = sectorColors[_targetSector - 1];
				break;
			case StripMode.Normal:
			case StripMode.Loop:
			default: {
				var toSend = ColorUtil.TruncateColors(ledColors.ToArray(), _offset, _ledCount, _multiplier);
				if (_data.ReverseStrip) {
					toSend = Enumerable.Reverse(toSend).ToArray();
				}

				int sumR = 0, sumG = 0, sumB = 0;
				foreach (var c in toSend) {
					sumR += c.R;
					sumG += c.G;
					sumB += c.B;
				}

				var count = Math.Max(toSend.Length, 1);
				target = Color.FromArgb(sumR / count, sumG / count, sumB / count);
				break;
			}
		}

		if (_ep != null) {
			await SendLanColor(target.R, target.G, target.B);
		} else if (!string.IsNullOrEmpty(_data.GoveeApiKey)) {
			await SendCloudColor(target.R, target.G, target.B);
		}
	}

	public Task ReloadData() {
		var dev = DataUtil.GetDevice<GoveeData>(Id);
		if (dev != null) {
			_data = dev;
		}

		_offset = _data.Offset;
		_brightness = _data.Brightness;
		Enable = _data.Enable;
		_stripMode = _data.StripMode;
		_targetSector = _data.TargetSector;
		_multiplier = _data.LedMultiplier;
		if (_multiplier == 0) _multiplier = 1;
		_ledCount = _data.LedCount;

		if (_data.LanEnabled && !string.IsNullOrEmpty(_data.IpAddress)) {
			_ep = IpUtil.Parse(_data.IpAddress, LanControlPort);
		}

		return Task.CompletedTask;
	}

	public void Dispose() {
		Dispose(true).ConfigureAwait(true);
		GC.SuppressFinalize(this);
	}

	private Task SetColors(object sender, ColorSendEventArgs args) {
		return SetColors(args.LedColors, args.SectorColors);
	}

	private void RefreshSystem() {
		ReloadData();
	}

	private async Task SetPower(bool on) {
		var cmd = new {
			msg = new {
				cmd = "turn",
				data = new { value = on ? 1 : 0 }
			}
		};

		await SendLanCommand(cmd);
	}

	private async Task SetBrightness(int brightness) {
		var value = Math.Clamp(brightness, 1, 100);
		var cmd = new {
			msg = new {
				cmd = "brightness",
				data = new { value }
			}
		};

		await SendLanCommand(cmd);
	}

	private async Task SendLanColor(int r, int g, int b) {
		var cmd = new {
			msg = new {
				cmd = "colorwc",
				data = new {
					color = new { r, g, b },
					colorTemInKelvin = 0
				}
			}
		};

		await SendLanCommand(cmd);
	}

	private async Task SendLanCommand(object command) {
		if (_ep == null) return;

		try {
			var json = JsonConvert.SerializeObject(command);
			var bytes = Encoding.UTF8.GetBytes(json);
			await _udpClient.SendAsync(bytes, bytes.Length, _ep).ConfigureAwait(false);
		} catch (Exception ex) {
			Log.Debug("Govee: LAN command error: " + ex.Message);
		}
	}

	private async Task SendCloudColor(int r, int g, int b) {
		try {
			var payload = new {
				requestId = Guid.NewGuid().ToString(),
				payload = new {
					sku = _data.Sku,
					device = _data.Id,
					capability = new {
						type = "devices.capabilities.color_setting",
						instance = "colorRgb",
						value = (r << 16) | (g << 8) | b
					}
				}
			};

			await SendCloudRequest(payload);
		} catch (Exception ex) {
			Log.Debug("Govee: Cloud color error: " + ex.Message);
		}
	}

	private async Task SendSegmentColors(IReadOnlyList<Color> sectorColors) {
		try {
			var segCount = _data.SegmentCount;
			var segments = new JArray();
			for (var i = 0; i < segCount && i < sectorColors.Count; i++) {
				var c = sectorColors[i];
				var colorVal = (c.R << 16) | (c.G << 8) | c.B;
				segments.Add(new JObject { ["pt"] = i, ["color"] = colorVal });
			}

			var payload = new {
				requestId = Guid.NewGuid().ToString(),
				payload = new {
					sku = _data.Sku,
					device = _data.Id,
					capability = new {
						type = "devices.capabilities.segment_color_setting",
						instance = "segmentedColorRgb",
						value = segments
					}
				}
			};

			await SendCloudRequest(payload);
		} catch (Exception ex) {
			Log.Debug("Govee: Segment color error: " + ex.Message);
		}
	}

	private async Task SendCloudRequest(object payload) {
		if (string.IsNullOrEmpty(_data.GoveeApiKey)) return;

		try {
			var json = JsonConvert.SerializeObject(payload);
			using var content = new StringContent(json, Encoding.UTF8, "application/json");
			using var request = new HttpRequestMessage(HttpMethod.Post, CloudControlUrl);
			request.Headers.Add("Govee-API-Key", _data.GoveeApiKey);
			request.Content = content;
			await HttpClient.SendAsync(request).ConfigureAwait(false);
		} catch (Exception ex) {
			Log.Debug("Govee: Cloud request error: " + ex.Message);
		}
	}

	private async Task Dispose(bool disposing) {
		if (_disposed) return;

		if (disposing) {
			if (Streaming) {
				await StopStream();
			}
		}

		_disposed = true;
	}
}
