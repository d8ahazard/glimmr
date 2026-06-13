#region

using System;
using System.Drawing;
using System.Security.Cryptography;
using Glimmr.Models.Data;
using Glimmr.Models.Util;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.HueSync;

/// <summary>
///     Process-wide shared state for the emulated Hue Bridge used by the Hue Sync color source.
///     The REST controller (per-request), the DTLS server, and the <see cref="HueSyncStream" />
///     all talk to this single instance so pairing, identity and decoded frames stay in sync.
/// </summary>
public sealed class HueSyncBridge {
	private static readonly Lazy<HueSyncBridge> LazyInstance = new(() => new HueSyncBridge());
	public static HueSyncBridge Instance => LazyInstance.Value;

	/// <summary>
	///     Raised whenever a full entertainment frame has been decoded. The argument is the
	///     per-channel color array (index = channel id).
	/// </summary>
	public event Action<Color[]>? FrameReceived;

	/// <summary>
	///     Raised when the entertainment stream starts/stops so the source can flip mode.
	/// </summary>
	public event Action<bool>? StreamStateChanged;

	private readonly object _lock = new();
	private DateTime _linkArmedUntil = DateTime.MinValue;

	public string BridgeId { get; private set; } = string.Empty;
	public string BridgeMac { get; private set; } = string.Empty;
	public string Username { get; private set; } = string.Empty;
	public string ClientKey { get; private set; } = string.Empty;
	public bool Streaming { get; private set; }

	private HueSyncBridge() {
		LoadIdentity();
	}

	/// <summary>
	///     The fixed UUID for the single entertainment configuration we expose.
	/// </summary>
	public string EntertainmentId => DeterministicGuid("ent-config");

	public bool IsLinkArmed {
		get {
			lock (_lock) {
				return DateTime.UtcNow < _linkArmedUntil;
			}
		}
	}

	public bool IsPaired => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(ClientKey);

	private void LoadIdentity() {
		var sd = DataUtil.GetSystemData();
		BridgeId = sd.HueSyncBridgeId;
		BridgeMac = sd.HueSyncBridgeMac;
		Username = sd.HueSyncUser;
		ClientKey = sd.HueSyncClientKey;

		var dirty = false;
		if (string.IsNullOrEmpty(BridgeMac)) {
			BridgeMac = GenerateMac();
			sd.HueSyncBridgeMac = BridgeMac;
			dirty = true;
		}

		if (string.IsNullOrEmpty(BridgeId)) {
			BridgeId = BridgeIdFromMac(BridgeMac);
			sd.HueSyncBridgeId = BridgeId;
			dirty = true;
		}

		if (dirty) {
			DataUtil.SetSystemData(sd);
		}

		Log.Debug($"HueSync: Emulated bridge identity loaded (id={BridgeId}, mac={BridgeMac}, paired={IsPaired}).");
	}

	/// <summary>
	///     Arm the "link button" so the next registration request from the Hue Sync app succeeds.
	/// </summary>
	public void ArmLink(int seconds = 60) {
		lock (_lock) {
			_linkArmedUntil = DateTime.UtcNow.AddSeconds(seconds);
		}

		Log.Information($"HueSync: Link armed for {seconds}s. Press 'connect' in the Hue Sync app now.");
	}

	public void DisarmLink() {
		lock (_lock) {
			_linkArmedUntil = DateTime.MinValue;
		}
	}

	/// <summary>
	///     Handle a registration (POST /api) request. Returns the new credentials when link mode
	///     is armed, otherwise null (caller should respond with the link-button error).
	/// </summary>
	public (string username, string clientKey)? Register() {
		if (!IsLinkArmed) {
			return null;
		}

		lock (_lock) {
			// Re-use existing credentials when already paired so the app's cached key keeps working.
			if (!IsPaired) {
				Username = GenerateHex(16); // 32 hex chars
				ClientKey = GenerateHex(16); // 32 hex chars (16-byte PSK)
				var sd = DataUtil.GetSystemData();
				sd.HueSyncUser = Username;
				sd.HueSyncClientKey = ClientKey;
				DataUtil.SetSystemData(sd);
			}

			_linkArmedUntil = DateTime.MinValue;
		}

		Log.Information("HueSync: Hue Sync app paired with emulated bridge.");
		return (Username, ClientKey);
	}

	/// <summary>
	///     Forget the paired app, forcing a fresh pairing next time.
	/// </summary>
	public void Unpair() {
		lock (_lock) {
			Username = string.Empty;
			ClientKey = string.Empty;
			var sd = DataUtil.GetSystemData();
			sd.HueSyncUser = string.Empty;
			sd.HueSyncClientKey = string.Empty;
			DataUtil.SetSystemData(sd);
		}
	}

	/// <summary>
	///     Returns the PSK bytes for a given identity, or null if the identity is unknown.
	///     Hue uses the registration username as the PSK identity and the clientkey as the PSK.
	/// </summary>
	public byte[]? GetPsk(string identity) {
		if (string.IsNullOrEmpty(ClientKey)) {
			return null;
		}

		// The Hue Sync app presents the username as the PSK identity.
		if (!string.Equals(identity, Username, StringComparison.OrdinalIgnoreCase)) {
			Log.Warning($"HueSync: Unknown PSK identity '{identity}'.");
			return null;
		}

		return HexToBytes(ClientKey);
	}

	public void SetStreaming(bool active) {
		if (Streaming == active) {
			return;
		}

		Streaming = active;
		Log.Information($"HueSync: Entertainment stream {(active ? "started" : "stopped")}.");
		StreamStateChanged?.Invoke(active);
	}

	public void PublishFrame(Color[] channelColors) {
		FrameReceived?.Invoke(channelColors);
	}

	#region helpers

	private static string GenerateMac() {
		var bytes = new byte[6];
		RandomNumberGenerator.Fill(bytes);
		// Locally-administered, unicast.
		bytes[0] = (byte)((bytes[0] & 0xFE) | 0x02);
		return string.Join(":", Array.ConvertAll(bytes, b => b.ToString("x2")));
	}

	private static string BridgeIdFromMac(string mac) {
		var hex = mac.Replace(":", "").ToUpperInvariant();
		if (hex.Length != 12) {
			hex = GenerateHex(6).ToUpperInvariant();
		}

		// Hue bridge id convention: first 6 + FFFE + last 6 of the MAC.
		return hex[..6] + "FFFE" + hex[6..];
	}

	private static string GenerateHex(int byteCount) {
		var bytes = new byte[byteCount];
		RandomNumberGenerator.Fill(bytes);
		return Convert.ToHexString(bytes).ToLowerInvariant();
	}

	private static byte[] HexToBytes(string hex) {
		return Convert.FromHexString(hex);
	}

	private string DeterministicGuid(string seed) {
		using var md5 = MD5.Create();
		var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(BridgeId + ":" + seed));
		var guid = new Guid(hash);
		return guid.ToString();
	}

	#endregion
}
