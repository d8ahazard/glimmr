#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Twinkly;

public class TwinklyDevice : ColorTarget, IColorTarget, IDisposable {
	private string IpAddress { get; set; }
	private const int UdpPort = 7777;

	private readonly UdpClient _udpClient;
	private TwinklyData _data;
	private bool _disposed;
	private IPEndPoint? _ep;
	private int _brightness;
	private int _ledCount;
	private int _targetSector;
	private string _authToken = "";
	private byte[] _tokenBytes = Array.Empty<byte>();

	public TwinklyDevice(TwinklyData td, ColorService cs) : base(cs) {
		_data = td ?? throw new ArgumentException("Invalid Twinkly data.");
		Id = _data.Id;
		IpAddress = _data.IpAddress;
		_brightness = _data.Brightness;
		_udpClient = cs.ControlService.UdpClient;
		cs.ControlService.RefreshSystemEvent += RefreshSystem;
		ReloadData();
		cs.ColorSendEventAsync += SetColors;
	}

	public bool Enable { get; set; }
	public bool Streaming { get; set; }
	public string Id { get; }

	IColorTargetData IColorTarget.Data {
		get => _data;
		set => _data = (TwinklyData)value;
	}

	public async Task StartStream(CancellationToken ct) {
		if (Streaming || !Enable) return;

		Log.Debug($"{_data.Tag}::Starting stream: {_data.Id}...");
		_ep = IpUtil.Parse(IpAddress, UdpPort);
		if (_ep == null) return;

		if (!await Authenticate()) {
			Log.Warning("Twinkly: Authentication failed for " + IpAddress);
			return;
		}

		await SetRealtimeMode();
		await SetDeviceBrightness(_brightness);
		Streaming = true;
		Log.Debug($"{_data.Tag}::Stream started: {_data.Id}.");
	}

	public async Task StopStream() {
		if (!Streaming) return;

		Log.Debug($"{_data.Tag}::Stopping stream...{_data.Id}.");
		Streaming = false;
		await SetMode("off");
		Log.Debug($"{_data.Tag}::Stream stopped: {_data.Id}.");
	}

	public async Task FlashColor(Color color) {
		if (_ep == null) return;
		try {
			var colors = ColorUtil.FillArray(color, _ledCount);
			var packet = BuildRealtimeFrame(colors);
			await _udpClient.SendAsync(packet, packet.Length, _ep).ConfigureAwait(false);
		} catch (Exception e) {
			Log.Debug("Twinkly: Exception flashing color: " + e.Message);
		}
	}

	public Task ReloadData() {
		var dev = DataUtil.GetDevice<TwinklyData>(Id);
		if (dev != null) _data = dev;
		_brightness = _data.Brightness;
		_ledCount = _data.LedCount;
		_targetSector = _data.TargetSector;
		IpAddress = _data.IpAddress;
		Enable = _data.Enable;
		return Task.CompletedTask;
	}

	public async Task SetColors(IReadOnlyList<Color> ledColors, IReadOnlyList<Color> sectorColors) {
		if (!Streaming || !Enable || _ep == null) return;

		Color[] toSend;
		if (_targetSector > 0 && _targetSector <= sectorColors.Count) {
			toSend = ColorUtil.FillArray(sectorColors[_targetSector - 1], _ledCount);
		} else {
			toSend = ColorUtil.TruncateColors(ledColors.ToArray(), 0, _ledCount);
		}

		try {
			var packet = BuildRealtimeFrame(toSend);
			await _udpClient.SendAsync(packet, packet.Length, _ep).ConfigureAwait(false);
		} catch (Exception e) {
			Log.Debug("Twinkly: Exception sending colors: " + e.Message);
		}
	}

	private Task SetColors(object sender, ColorSendEventArgs args) {
		return SetColors(args.LedColors, args.SectorColors);
	}

	private byte[] BuildRealtimeFrame(IReadOnlyList<Color> colors) {
		// Version byte (0x03), token bytes, then RGB triplets
		var rgbLen = colors.Count * 3;
		var packet = new byte[1 + _tokenBytes.Length + rgbLen];
		packet[0] = 0x03;
		Array.Copy(_tokenBytes, 0, packet, 1, _tokenBytes.Length);
		var offset = 1 + _tokenBytes.Length;
		for (var i = 0; i < colors.Count; i++) {
			packet[offset + i * 3] = colors[i].R;
			packet[offset + i * 3 + 1] = colors[i].G;
			packet[offset + i * 3 + 2] = colors[i].B;
		}

		return packet;
	}

	private async Task<bool> Authenticate() {
		try {
			using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
			var challenge = Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("N")[..16]));
			var loginBody = JsonConvert.SerializeObject(new { challenge });
			using var loginContent = new StringContent(loginBody, Encoding.UTF8, "application/json");
			var loginResp = await http.PostAsync($"http://{IpAddress}/xled/v1/login", loginContent);
			var loginJson = await loginResp.Content.ReadAsStringAsync();
			var loginResult = JsonConvert.DeserializeObject<TwinklyLoginResponse>(loginJson);
			if (loginResult == null || string.IsNullOrEmpty(loginResult.AuthenticationToken)) return false;

			_authToken = loginResult.AuthenticationToken;
			_tokenBytes = Convert.FromBase64String(_authToken);

			var verifyBody = JsonConvert.SerializeObject(new { challenge_response = loginResult.ChallengeResponse });
			using var verifyContent = new StringContent(verifyBody, Encoding.UTF8, "application/json");
			http.DefaultRequestHeaders.Add("X-Auth-Token", _authToken);
			await http.PostAsync($"http://{IpAddress}/xled/v1/verify", verifyContent);

			return true;
		} catch (Exception e) {
			Log.Warning("Twinkly: Auth error: " + e.Message);
			return false;
		}
	}

	private async Task SetRealtimeMode() {
		await SetMode("rt");
	}

	private async Task SetMode(string mode) {
		try {
			using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
			http.DefaultRequestHeaders.Add("X-Auth-Token", _authToken);
			var body = JsonConvert.SerializeObject(new { mode });
			using var content = new StringContent(body, Encoding.UTF8, "application/json");
			await http.PostAsync($"http://{IpAddress}/xled/v1/led/mode", content);
		} catch (Exception e) {
			Log.Debug("Twinkly: Exception setting mode: " + e.Message);
		}
	}

	private async Task SetDeviceBrightness(int brightness) {
		try {
			using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
			http.DefaultRequestHeaders.Add("X-Auth-Token", _authToken);
			var body = JsonConvert.SerializeObject(new { mode = "enabled", type = "A", value = brightness });
			using var content = new StringContent(body, Encoding.UTF8, "application/json");
			await http.PostAsync($"http://{IpAddress}/xled/v1/led/out/brightness", content);
		} catch (Exception e) {
			Log.Debug("Twinkly: Exception setting brightness: " + e.Message);
		}
	}

	private void RefreshSystem() {
		ReloadData();
	}

	public void Dispose() {
		Dispose(true).ConfigureAwait(true);
		GC.SuppressFinalize(this);
	}

	protected virtual async Task Dispose(bool disposing) {
		if (_disposed) return;
		if (disposing && Streaming) {
			await StopStream();
		}

		_disposed = true;
	}
}

internal class TwinklyLoginResponse {
	[JsonProperty("authentication_token")]
	public string AuthenticationToken { get; set; } = "";

	[JsonProperty("challenge-response")]
	public string ChallengeResponse { get; set; } = "";
}
