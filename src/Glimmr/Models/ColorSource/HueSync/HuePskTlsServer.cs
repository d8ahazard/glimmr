extern alias bc2;

#region

using System.Text;
using bc2::Org.BouncyCastle.Tls;
using bc2::Org.BouncyCastle.Tls.Crypto;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.HueSync;

/// <summary>
///     PSK-only DTLS 1.2 server restricted to the single cipher suite Hue uses
///     (TLS_PSK_WITH_AES_128_GCM_SHA256). The pre-shared key is resolved per-identity from the
///     paired Hue Sync credentials held by <see cref="HueSyncBridge" />.
/// </summary>
public sealed class HuePskTlsServer : PskTlsServer {
	public HuePskTlsServer(TlsCrypto crypto, HueSyncBridge bridge)
		: base(crypto, new HuePskIdentityManager(bridge)) {
	}

	protected override ProtocolVersion[] GetSupportedVersions() {
		return ProtocolVersion.DTLSv12.Only();
	}

	protected override int[] GetSupportedCipherSuites() {
		return new[] { CipherSuite.TLS_PSK_WITH_AES_128_GCM_SHA256 };
	}
}

/// <summary>
///     Resolves the DTLS pre-shared key for a presented identity using the paired Hue Sync key.
/// </summary>
internal sealed class HuePskIdentityManager : TlsPskIdentityManager {
	private readonly HueSyncBridge _bridge;

	public HuePskIdentityManager(HueSyncBridge bridge) {
		_bridge = bridge;
	}

	public byte[]? GetHint() {
		return null;
	}

	public byte[]? GetPsk(byte[] identity) {
		var id = identity == null ? string.Empty : Encoding.UTF8.GetString(identity);
		var psk = _bridge.GetPsk(id);
		if (psk == null) {
			Log.Warning("HueSync: No PSK available for presented identity.");
		}

		return psk;
	}
}
