#region

using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Serilog;

#endregion

namespace Glimmr.Models.Util; 

public static class IpUtil {
	private static string? _localIp;

	public static IPEndPoint? Parse(string endpoint, int portIn) {
		if (string.IsNullOrEmpty(endpoint)
		    || endpoint.Trim().Length == 0) {
			throw new ArgumentException("Endpoint descriptor may not be empty.");
		}

		if (portIn != -1 &&
		    portIn is < IPEndPoint.MinPort or > IPEndPoint.MaxPort) {
			throw new ArgumentException($"Invalid default port '{portIn}'");
		}

		var values = endpoint.Split(new[] { ':' });
		IPAddress? ipAddress;
		int port;

		switch (values.Length) {
			//check if we have an IPv6 or ports
			// ipv4 or hostname
			case <= 2: {
				port = values.Length == 1 ? portIn : GetPort(values[1]);

				//try to use the address as IPv4, otherwise get hostname
				if (!IPAddress.TryParse(values[0], out ipAddress)) {
					ipAddress = GetIpFromHost(values[0]);
				}

				break;
			}
			//ipv6
			//could [a:b:c]:d
			case > 2 when values[0].StartsWith("[") && values[^2].EndsWith("]"): {
				var ipString
					= string.Join(":", values.Take(values.Length - 1).ToArray());
				ipAddress = IPAddress.Parse(ipString);
				port = GetPort(values[^1]);
				break;
			}
			//[a:b:c] or a:b:c
			case > 2:
				ipAddress = IPAddress.Parse(endpoint);
				port = portIn;
				break;
		}

		if (port == -1) {
			throw new ArgumentException($"No port specified: '{endpoint}'");
		}

		return ipAddress != null ? new IPEndPoint(ipAddress, port) : null;
	}

	private static int GetPort(string p) {
		if (!int.TryParse(p, out var port)
		    || port is < IPEndPoint.MinPort or > IPEndPoint.MaxPort) {
			throw new FormatException($@"Invalid end point port '{p}'");
		}

		return port;
	}

	public static IPAddress? GetIpFromHost(string p) {
		if (string.IsNullOrEmpty(p)) {
			return null;
		}

		//DUH
		if (IPAddress.TryParse(p, out var parsed)) {
			return parsed;
		}

		try {
			var hosts = Dns.GetHostAddresses(p);

			// Use the first address like always if found
			foreach (var host in hosts) {
				if (host.AddressFamily == AddressFamily.InterNetwork) {
					return host;
				}
			}
		} catch {
			// ignored
		}

		try {
			// If not, try this
			var dns = Dns.GetHostEntry(p);
			foreach (var host in dns.AddressList) {
				if (host.AddressFamily == AddressFamily.InterNetwork) {
					return host;
				}
			}
		} catch {
			// ignored
		}

		try {
			// If still no dice, try getting appending .local
			var dns = Dns.GetHostEntry(p + ".local");
			foreach (var host in dns.AddressList) {
				if (host.AddressFamily == AddressFamily.InterNetwork) {
					return host;
				}
			}
		} catch (Exception e) {
			Log.Debug($"Exception for {p}: " + e.Message);
		}

		return null;
	}

	private static string GetOsxHostName() {
		var result = "";
		try {
			var process = new Process {
				StartInfo = new ProcessStartInfo {
					FileName = "/bin/sh",
					Arguments = "-c \"hostname\"",
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true
				}
			};
			process.Start();
			while (!process.StandardOutput.EndOfStream) {
				var res = process.StandardOutput.ReadLine();
				if (!string.IsNullOrEmpty(res)) {
					result = res;
				}
			}

			process.Dispose();
		} catch (Exception e) {
			Log.Warning("Exception: " + e.Message);
		}

		Log.Debug("OSX Hostname: " + result);
		return result;
	}

	public static string GetLocalOsxIpAddress() {
		var result = "";
		try {
			var process = new Process {
				StartInfo = new ProcessStartInfo {
					FileName = "ipconfig",
					Arguments = "getifaddr en0",
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true
				}
			};
			process.Start();
			while (!process.StandardOutput.EndOfStream) {
				var res = process.StandardOutput.ReadLine();
				if (!string.IsNullOrEmpty(res)) {
					result = res;
				}
			}

			process.Dispose();
		} catch (Exception e) {
			Log.Warning("Exception: " + e.Message);
		}

		if (!string.IsNullOrEmpty(result)) {
			_localIp = result;
		}

		Log.Debug("OSX IP: " + result);
		return result;
	}

	public static string GetLocalIpAddress() {
		if (!string.IsNullOrEmpty(_localIp)) {
			return _localIp;
		}

		var res = "";
		var hostName = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? GetOsxHostName() : Dns.GetHostName();
		if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
			return GetLocalOsxIpAddress();
		}

		try {
			if (!string.IsNullOrEmpty(hostName)) {
				var host = Dns.GetHostEntry(hostName);
				foreach (var ip in host.AddressList) {
					if (ip.AddressFamily != AddressFamily.InterNetwork) {
						continue;
					}

					if (IPAddress.IsLoopback(ip)) {
						continue;
					}

					res = ip.ToString();
					break;
				}
			}
		} catch (Exception e) {
			Log.Warning("Exception getting host IP: " + e.Message);
		}

		try {
			hostName += ".local";
			if (!string.IsNullOrEmpty(hostName)) {
				var host = Dns.GetHostEntry(hostName);
				foreach (var ip in host.AddressList) {
					if (ip.AddressFamily != AddressFamily.InterNetwork) {
						continue;
					}

					res = ip.ToString();
					break;
				}
			}
		} catch (Exception e) {
			Log.Warning("Exception getting host IP: " + e.Message);
		}

		_localIp = res;
		return res;
	}
}