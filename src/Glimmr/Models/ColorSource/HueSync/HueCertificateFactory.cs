#region

using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Glimmr.Models.Util;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.HueSync;

/// <summary>
///     Creates (and persists) the self-signed certificate the emulated bridge presents over HTTPS.
///     The Hue Sync app does not validate the certificate chain but expects a stable cert whose
///     common name matches the bridge id, so we generate an ECDSA P-256 cert once and cache it as a
///     PFX in the Glimmr user directory. Changing this cert after pairing forces the app to re-pair.
/// </summary>
public static class HueCertificateFactory {
	private const string PfxFileName = "huesync_bridge.pfx";

	// Fixed passphrase for the on-disk PFX; this only guards a local file, not a secret.
	private static readonly char[] PfxPassword = "glimmr".ToCharArray();

	private static X509Certificate2? _cached;
	private static readonly object Lock = new();

	public static X509Certificate2 CreateOrLoad() {
		lock (Lock) {
			if (_cached != null) {
				return _cached;
			}

			var bridge = HueSyncBridge.Instance;
			var path = PfxPath();

			if (!string.IsNullOrEmpty(path) && File.Exists(path)) {
				try {
					_cached = LoadPfx(path);
					return _cached;
				} catch (Exception e) {
					Log.Warning("HueSync: Failed to load cached bridge certificate, regenerating: " + e.Message);
				}
			}

			_cached = Generate(bridge.BridgeId);
			Persist(_cached, path);
			return _cached;
		}
	}

	private static X509Certificate2 Generate(string bridgeId) {
		using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
		var subject = $"CN={bridgeId}, O=Philips Hue, C=NL";
		var request = new CertificateRequest(subject, ecdsa, HashAlgorithmName.SHA256);

		request.CertificateExtensions.Add(
			new X509BasicConstraintsExtension(false, false, 0, false));
		request.CertificateExtensions.Add(
			new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));

		var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
		var notAfter = DateTimeOffset.UtcNow.AddYears(20);
		var cert = request.CreateSelfSigned(notBefore, notAfter);
		Log.Information($"HueSync: Generated emulated bridge certificate (CN={bridgeId}).");
		return cert;
	}

	private static void Persist(X509Certificate2 cert, string path) {
		if (string.IsNullOrEmpty(path)) {
			return;
		}

		try {
			var bytes = cert.Export(X509ContentType.Pfx, new string(PfxPassword));
			File.WriteAllBytes(path, bytes);
		} catch (Exception e) {
			Log.Warning("HueSync: Failed to persist bridge certificate: " + e.Message);
		}
	}

	private static X509Certificate2 LoadPfx(string path) {
		// Mark as exportable + persistable so Kestrel can use the private key for TLS.
		return new X509Certificate2(path, new string(PfxPassword),
			X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet);
	}

	private static string PfxPath() {
		var dir = SystemUtil.GetUserDir();
		return string.IsNullOrEmpty(dir) ? string.Empty : Path.Combine(dir, PfxFileName);
	}
}
