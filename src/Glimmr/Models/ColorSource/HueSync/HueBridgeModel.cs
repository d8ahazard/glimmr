#region

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Glimmr.Models.Data;
using Glimmr.Models.Util;

#endregion

namespace Glimmr.Models.ColorSource.HueSync;

/// <summary>
///     Builds the JSON payloads our emulated Hue Bridge returns to the Hue Sync app for both the
///     legacy v1 ("/api") and CLIP v2 ("/clip/v2") surfaces. A single entertainment configuration is
///     exposed whose channel count matches Glimmr's sector grid, so channel i maps to sector i.
/// </summary>
public static class HueBridgeModel {
	private const string ModelId = "BSB002"; // Hue Bridge v2 model id
	private const string SwVersion = "1968096020";
	private const string ApiVersion = "1.68.0";

	/// <summary>
	///     Unauthenticated short config returned from GET /api/config.
	/// </summary>
	public static Dictionary<string, object> ShortConfig(HueSyncBridge bridge) {
		var sd = DataUtil.GetSystemData();
		return new Dictionary<string, object> {
			["name"] = BridgeName(sd),
			["datastoreversion"] = "131",
			["swversion"] = SwVersion,
			["apiversion"] = ApiVersion,
			["mac"] = bridge.BridgeMac,
			["bridgeid"] = bridge.BridgeId,
			["factorynew"] = false,
			["replacesbridgeid"] = null!,
			["modelid"] = ModelId,
			["starterkitid"] = ""
		};
	}

	public static Dictionary<string, object> FullConfig(HueSyncBridge bridge) {
		var sd = DataUtil.GetSystemData();
		var ip = sd.IpAddress;
		var whitelist = new Dictionary<string, object>();
		if (!string.IsNullOrEmpty(bridge.Username)) {
			whitelist[bridge.Username] = new Dictionary<string, object> {
				["last use date"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss"),
				["create date"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss"),
				["name"] = "Hue Sync#Glimmr"
			};
		}

		return new Dictionary<string, object> {
			["name"] = BridgeName(sd),
			["zigbeechannel"] = 25,
			["bridgeid"] = bridge.BridgeId,
			["mac"] = bridge.BridgeMac,
			["dhcp"] = true,
			["ipaddress"] = ip,
			["netmask"] = "255.255.255.0",
			["gateway"] = GatewayGuess(ip),
			["proxyaddress"] = "none",
			["proxyport"] = 0,
			["modelid"] = ModelId,
			["datastoreversion"] = "131",
			["swversion"] = SwVersion,
			["apiversion"] = ApiVersion,
			["linkbutton"] = bridge.IsLinkArmed,
			["portalservices"] = false,
			["factorynew"] = false,
			["replacesbridgeid"] = null!,
			["starterkitid"] = "",
			["whitelist"] = whitelist
		};
	}

	/// <summary>
	///     v1 lights collection - one virtual light per sector.
	/// </summary>
	public static Dictionary<string, object> Lights(HueSyncBridge bridge, int sectorCount) {
		var lights = new Dictionary<string, object>();
		for (var i = 0; i < sectorCount; i++) {
			lights[(i + 1).ToString()] = new Dictionary<string, object> {
				["state"] = new Dictionary<string, object> {
					["on"] = true,
					["bri"] = 254,
					["hue"] = 0,
					["sat"] = 0,
					["xy"] = new[] { 0.0, 0.0 },
					["ct"] = 153,
					["alert"] = "none",
					["colormode"] = "xy",
					["mode"] = "homeautomation",
					["reachable"] = true
				},
				["type"] = "Extended color light",
				["name"] = $"Glimmr Sector {i + 1}",
				["modelid"] = "LCT015",
				["manufacturername"] = "Signify Netherlands B.V.",
				["productname"] = "Hue color lamp",
				["uniqueid"] = SectorMac(bridge, i),
				["swversion"] = "1.0.0"
			};
		}

		return lights;
	}

	/// <summary>
	///     v1 groups collection containing our single entertainment group.
	/// </summary>
	public static Dictionary<string, object> Groups(HueSyncBridge bridge, int sectorCount) {
		return new Dictionary<string, object> {
			["1"] = EntertainmentGroupV1(bridge, sectorCount)
		};
	}

	public static Dictionary<string, object> EntertainmentGroupV1(HueSyncBridge bridge, int sectorCount) {
		var lights = new List<string>();
		var locations = new Dictionary<string, object>();
		for (var i = 0; i < sectorCount; i++) {
			var id = (i + 1).ToString();
			lights.Add(id);
			var (x, y, z) = RingPosition(i, sectorCount);
			locations[id] = new[] { x, y, z };
		}

		return new Dictionary<string, object> {
			["name"] = "Glimmr",
			["lights"] = lights,
			["type"] = "Entertainment",
			["class"] = "TV",
			["stream"] = new Dictionary<string, object> {
				["proxymode"] = "auto",
				["proxynode"] = "/bridge",
				["active"] = bridge.Streaming,
				["owner"] = bridge.Username
			},
			["locations"] = locations,
			["state"] = new Dictionary<string, object> { ["all_on"] = true, ["any_on"] = true }
		};
	}

	/// <summary>
	///     CLIP v2 entertainment_configuration resource (single area).
	/// </summary>
	public static Dictionary<string, object> EntertainmentConfigurationV2(HueSyncBridge bridge, int sectorCount) {
		var channels = new List<object>();
		var serviceLocations = new List<object>();
		for (var i = 0; i < sectorCount; i++) {
			var (x, y, z) = RingPosition(i, sectorCount);
			var entId = EntertainmentServiceId(bridge, i);
			channels.Add(new Dictionary<string, object> {
				["channel_id"] = i,
				["position"] = new Dictionary<string, object> { ["x"] = x, ["y"] = y, ["z"] = z },
				["members"] = new[] {
					new Dictionary<string, object> {
						["service"] = new Dictionary<string, object> {
							["rid"] = entId, ["rtype"] = "entertainment"
						},
						["index"] = 0
					}
				}
			});

			serviceLocations.Add(new Dictionary<string, object> {
				["service"] = new Dictionary<string, object> { ["rid"] = entId, ["rtype"] = "entertainment" },
				["positions"] = new[] {
					new Dictionary<string, object> { ["x"] = x, ["y"] = y, ["z"] = z }
				}
			});
		}

		return new Dictionary<string, object> {
			["id"] = bridge.EntertainmentId,
			["id_v1"] = "/groups/1",
			["type"] = "entertainment_configuration",
			["metadata"] = new Dictionary<string, object> { ["name"] = "Glimmr" },
			["name"] = "Glimmr",
			["configuration_type"] = "screen",
			["status"] = bridge.Streaming ? "active" : "inactive",
			["active_streamer"] = bridge.Streaming
				? new Dictionary<string, object> { ["rid"] = bridge.Username, ["rtype"] = "auth_v1" }
				: null!,
			["stream_proxy"] = new Dictionary<string, object> {
				["mode"] = "auto",
				["node"] = new Dictionary<string, object> {
					["rid"] = EntertainmentServiceId(bridge, 0), ["rtype"] = "entertainment"
				}
			},
			["channels"] = channels,
			["locations"] = new Dictionary<string, object> { ["service_locations"] = serviceLocations },
			["light_services"] = LightServiceRefs(bridge, sectorCount)
		};
	}

	public static List<object> EntertainmentServicesV2(HueSyncBridge bridge, int sectorCount) {
		var services = new List<object>();
		for (var i = 0; i < sectorCount; i++) {
			services.Add(new Dictionary<string, object> {
				["id"] = EntertainmentServiceId(bridge, i),
				["type"] = "entertainment",
				["owner"] = new Dictionary<string, object> {
					["rid"] = DeviceId(bridge, i), ["rtype"] = "device"
				},
				["renderer"] = true,
				["proxy"] = i == 0,
				["max_streams"] = 1,
				["segments"] = new Dictionary<string, object> {
					["configurable"] = false,
					["max_segments"] = 1,
					["segments"] = new[] {
						new Dictionary<string, object> { ["start"] = 0, ["length"] = 1 }
					}
				}
			});
		}

		return services;
	}

	public static List<object> DevicesV2(HueSyncBridge bridge, int sectorCount) {
		var devices = new List<object>();
		for (var i = 0; i < sectorCount; i++) {
			devices.Add(new Dictionary<string, object> {
				["id"] = DeviceId(bridge, i),
				["type"] = "device",
				["product_data"] = new Dictionary<string, object> {
					["model_id"] = "LCT015",
					["manufacturer_name"] = "Signify Netherlands B.V.",
					["product_name"] = "Hue color lamp",
					["product_archetype"] = "sultan_bulb",
					["certified"] = true,
					["software_version"] = "1.0.0"
				},
				["metadata"] = new Dictionary<string, object> {
					["name"] = $"Glimmr Sector {i + 1}", ["archetype"] = "sultan_bulb"
				},
				["services"] = new[] {
					new Dictionary<string, object> {
						["rid"] = LightId(bridge, i), ["rtype"] = "light"
					},
					new Dictionary<string, object> {
						["rid"] = EntertainmentServiceId(bridge, i), ["rtype"] = "entertainment"
					}
				}
			});
		}

		return devices;
	}

	public static List<object> LightsV2(HueSyncBridge bridge, int sectorCount) {
		var lights = new List<object>();
		for (var i = 0; i < sectorCount; i++) {
			lights.Add(new Dictionary<string, object> {
				["id"] = LightId(bridge, i),
				["id_v1"] = $"/lights/{i + 1}",
				["type"] = "light",
				["owner"] = new Dictionary<string, object> {
					["rid"] = DeviceId(bridge, i), ["rtype"] = "device"
				},
				["metadata"] = new Dictionary<string, object> {
					["name"] = $"Glimmr Sector {i + 1}", ["archetype"] = "sultan_bulb"
				},
				["on"] = new Dictionary<string, object> { ["on"] = true },
				["dimming"] = new Dictionary<string, object> { ["brightness"] = 100.0 },
				["color"] = new Dictionary<string, object> {
					["gamut_type"] = "C",
					["xy"] = new Dictionary<string, object> { ["x"] = 0.3, ["y"] = 0.3 }
				},
				["color_temperature"] = new Dictionary<string, object> { ["mirek"] = 366 }
			});
		}

		return lights;
	}

	/// <summary>
	///     Standard CLIP v2 envelope: { "errors": [], "data": [ ... ] }.
	/// </summary>
	public static Dictionary<string, object> V2Envelope(object data) {
		return new Dictionary<string, object> {
			["errors"] = Array.Empty<object>(),
			["data"] = data
		};
	}

	private static List<object> LightServiceRefs(HueSyncBridge bridge, int sectorCount) {
		var refs = new List<object>();
		for (var i = 0; i < sectorCount; i++) {
			refs.Add(new Dictionary<string, object> {
				["rid"] = LightId(bridge, i), ["rtype"] = "light"
			});
		}

		return refs;
	}

	private static string BridgeName(SystemData sd) {
		return string.IsNullOrEmpty(sd.DeviceName) ? "Glimmr" : sd.DeviceName;
	}

	private static (double x, double y, double z) RingPosition(int index, int count) {
		if (count <= 0) {
			return (0, 0, 0);
		}

		var angle = 2.0 * Math.PI * index / count;
		var x = Math.Round(Math.Cos(angle), 4);
		var z = Math.Round(Math.Sin(angle), 4);
		return (x, 0.0, z);
	}

	private static string GatewayGuess(string ip) {
		var idx = ip.LastIndexOf('.');
		return idx > 0 ? ip[..idx] + ".1" : "192.168.1.1";
	}

	private static string SectorMac(HueSyncBridge bridge, int index) {
		var baseMac = bridge.BridgeMac.Replace(":", "");
		if (baseMac.Length < 12) {
			baseMac = baseMac.PadRight(12, '0');
		}

		return $"{baseMac[..2]}:{baseMac[2..4]}:{baseMac[4..6]}:{baseMac[6..8]}:{baseMac[8..10]}:{baseMac[10..12]}-{index + 1:x2}";
	}

	private static string EntertainmentServiceId(HueSyncBridge bridge, int index) {
		return DeterministicGuid(bridge, $"entertainment-{index}");
	}

	private static string DeviceId(HueSyncBridge bridge, int index) {
		return DeterministicGuid(bridge, $"device-{index}");
	}

	private static string LightId(HueSyncBridge bridge, int index) {
		return DeterministicGuid(bridge, $"light-{index}");
	}

	private static string DeterministicGuid(HueSyncBridge bridge, string seed) {
		using var md5 = MD5.Create();
		var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(bridge.BridgeId + ":" + seed));
		return new Guid(hash).ToString();
	}
}
