#region

using System.Collections.Generic;
using Glimmr.Models.ColorSource.HueSync;
using Glimmr.Models.Util;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

#endregion

namespace Glimmr.Controllers;

/// <summary>
///     Emulates the subset of the Philips Hue Bridge REST API that the Hue Sync TV/mobile app needs
///     to discover, pair with, and start streaming entertainment data to Glimmr. The actual color
///     stream arrives over DTLS (see <see cref="HueDtlsServer" />); these endpoints only handle
///     discovery, pairing, and entertainment-area selection/activation.
/// </summary>
[ApiController]
public class HueBridgeController : ControllerBase {
	private static HueSyncBridge Bridge => HueSyncBridge.Instance;

	private static int SectorCount {
		get {
			var sd = DataUtil.GetSystemData();
			return sd.SectorCount;
		}
	}

	private ContentResult Hue(object payload) {
		return Content(JsonConvert.SerializeObject(payload), "application/json");
	}

	/// <summary>
	///     UPnP device description used during SSDP discovery.
	/// </summary>
	[HttpGet("description.xml")]
	[Produces("application/xml")]
	public ContentResult Description() {
		var sd = DataUtil.GetSystemData();
		var ip = sd.IpAddress;
		var xml =
			"<?xml version=\"1.0\" encoding=\"UTF-8\" ?>" +
			"<root xmlns=\"urn:schemas-upnp-org:device-1-0\">" +
			"<specVersion><major>1</major><minor>0</minor></specVersion>" +
			$"<URLBase>http://{ip}:80/</URLBase>" +
			"<device>" +
			"<deviceType>urn:schemas-upnp-org:device:Basic:1</deviceType>" +
			$"<friendlyName>Glimmr ({ip})</friendlyName>" +
			"<manufacturer>Signify</manufacturer>" +
			"<manufacturerURL>http://www.philips-hue.com</manufacturerURL>" +
			"<modelDescription>Philips hue Personal Wireless Lighting</modelDescription>" +
			"<modelName>Philips hue bridge 2015</modelName>" +
			"<modelNumber>BSB002</modelNumber>" +
			$"<serialNumber>{Bridge.BridgeId}</serialNumber>" +
			$"<UDN>uuid:2f402f80-da50-11e1-9b23-{Bridge.BridgeMac.Replace(":", "")}</UDN>" +
			"</device></root>";
		return Content(xml, "application/xml");
	}

	/// <summary>
	///     Unauthenticated bridge config (GET /api/config).
	/// </summary>
	[HttpGet("api/config")]
	public ContentResult ShortConfig() {
		return Hue(HueBridgeModel.ShortConfig(Bridge));
	}

	/// <summary>
	///     Registration / link-button pairing (POST /api).
	/// </summary>
	[HttpPost("api")]
	public ContentResult Register([FromBody] JObject? body) {
		var deviceType = body?["devicetype"]?.ToString() ?? "unknown";
		Log.Information($"HueSync: Registration request from '{deviceType}'.");

		var result = Bridge.Register();
		if (result == null) {
			return Hue(new[] {
				new Dictionary<string, object> {
					["error"] = new Dictionary<string, object> {
						["type"] = 101,
						["address"] = "/",
						["description"] = "link button not pressed"
					}
				}
			});
		}

		var success = new Dictionary<string, object> {
			["username"] = result.Value.username
		};
		// Only include the client key when the app asked for it (entertainment apps always do).
		if (body?["generateclientkey"]?.Value<bool>() ?? true) {
			success["clientkey"] = result.Value.clientKey;
		}

		return Hue(new[] {
			new Dictionary<string, object> { ["success"] = success }
		});
	}

	/// <summary>
	///     Full datastore (GET /api/{user}).
	/// </summary>
	[HttpGet("api/{user}")]
	public ContentResult DataStore(string user) {
		var count = SectorCount;
		return Hue(new Dictionary<string, object> {
			["lights"] = HueBridgeModel.Lights(Bridge, count),
			["groups"] = HueBridgeModel.Groups(Bridge, count),
			["config"] = HueBridgeModel.FullConfig(Bridge),
			["schedules"] = new Dictionary<string, object>(),
			["scenes"] = new Dictionary<string, object>(),
			["rules"] = new Dictionary<string, object>(),
			["sensors"] = new Dictionary<string, object>(),
			["resourcelinks"] = new Dictionary<string, object>()
		});
	}

	[HttpGet("api/{user}/config")]
	public ContentResult UserConfig(string user) {
		return Hue(HueBridgeModel.FullConfig(Bridge));
	}

	[HttpGet("api/{user}/lights")]
	public ContentResult UserLights(string user) {
		return Hue(HueBridgeModel.Lights(Bridge, SectorCount));
	}

	[HttpGet("api/{user}/groups")]
	public ContentResult UserGroups(string user) {
		return Hue(HueBridgeModel.Groups(Bridge, SectorCount));
	}

	[HttpGet("api/{user}/groups/{id}")]
	public ContentResult UserGroup(string user, string id) {
		return Hue(HueBridgeModel.EntertainmentGroupV1(Bridge, SectorCount));
	}

	/// <summary>
	///     v1 entertainment stream activation (PUT /api/{user}/groups/{id}).
	///     The actual stream start is detected by the DTLS handshake; this just acks intent.
	/// </summary>
	[HttpPut("api/{user}/groups/{id}")]
	public ContentResult UpdateGroup(string user, string id, [FromBody] JObject? body) {
		var responses = new List<object>();
		var stream = body?["stream"] as JObject;
		if (stream?["active"] != null) {
			var active = stream["active"]!.Value<bool>();
			Log.Information($"HueSync: v1 group {id} stream active={active}.");
			responses.Add(new Dictionary<string, object> {
				["success"] = new Dictionary<string, object> {
					[$"/groups/{id}/stream/active"] = active
				}
			});
		}

		return Hue(responses);
	}

	#region CLIP v2

	[HttpGet("clip/v2/resource/entertainment_configuration")]
	public ContentResult EntConfigs() {
		return Hue(HueBridgeModel.V2Envelope(new[] {
			HueBridgeModel.EntertainmentConfigurationV2(Bridge, SectorCount)
		}));
	}

	[HttpGet("clip/v2/resource/entertainment_configuration/{id}")]
	public ContentResult EntConfig(string id) {
		return Hue(HueBridgeModel.V2Envelope(new[] {
			HueBridgeModel.EntertainmentConfigurationV2(Bridge, SectorCount)
		}));
	}

	/// <summary>
	///     v2 entertainment stream activation (PUT /clip/v2/resource/entertainment_configuration/{id}).
	/// </summary>
	[HttpPut("clip/v2/resource/entertainment_configuration/{id}")]
	public ContentResult UpdateEntConfig(string id, [FromBody] JObject? body) {
		var action = body?["action"]?.ToString();
		if (!string.IsNullOrEmpty(action)) {
			Log.Information($"HueSync: v2 entertainment_configuration action={action}.");
		}

		return Hue(HueBridgeModel.V2Envelope(new[] {
			new Dictionary<string, object> {
				["rid"] = Bridge.EntertainmentId, ["rtype"] = "entertainment_configuration"
			}
		}));
	}

	[HttpGet("clip/v2/resource/entertainment")]
	public ContentResult EntServices() {
		return Hue(HueBridgeModel.V2Envelope(HueBridgeModel.EntertainmentServicesV2(Bridge, SectorCount)));
	}

	[HttpGet("clip/v2/resource/device")]
	public ContentResult Devices() {
		return Hue(HueBridgeModel.V2Envelope(HueBridgeModel.DevicesV2(Bridge, SectorCount)));
	}

	[HttpGet("clip/v2/resource/light")]
	public ContentResult LightsV2() {
		return Hue(HueBridgeModel.V2Envelope(HueBridgeModel.LightsV2(Bridge, SectorCount)));
	}

	#endregion
}
