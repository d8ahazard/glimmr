using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using HueSyncTestClient;

// ---------------------------------------------------------------------------
// Hue Sync loopback test client.
//
// Emulates what the Philips Hue Sync TV app does to a bridge: pairs over the
// REST API to obtain a DTLS client key, opens a DTLS-PSK entertainment session
// to UDP 2100, and streams animated HueStream v2 frames. Point it at a running
// Glimmr instance to validate the full receive -> decode -> drive-devices path
// without needing a real TV.
//
// Usage:
//   dotnet run --project tools/HueSyncTestClient -- [host] [seconds] [channels]
//   defaults: host=127.0.0.1, seconds=30, channels=auto (from bridge)
// ---------------------------------------------------------------------------

var host = args.Length > 0 ? args[0] : "127.0.0.1";
var seconds = args.Length > 1 && int.TryParse(args[1], out var s) ? s : 30;
int? channelsOverride = args.Length > 2 && int.TryParse(args[2], out var c) ? c : null;

Console.WriteLine($"Hue Sync test client -> {host} for {seconds}s");

using var http = new HttpClient { BaseAddress = new Uri($"http://{host}") };

// 1) Arm the bridge link button.
Console.WriteLine("Arming bridge link...");
await http.GetAsync("/api/Glimmr/hueSyncLink");
await Task.Delay(500);

// 2) Register to obtain username + clientkey (the DTLS PSK).
Console.WriteLine("Registering with bridge...");
var registerResp = await http.PostAsJsonAsync("/api", new {
	devicetype = "HueSyncTestClient#loopback",
	generateclientkey = true
});

var registerJson = await registerResp.Content.ReadAsStringAsync();
var creds = HueRegistration.Parse(registerJson);
if (creds == null) {
	Console.WriteLine("Pairing failed. Response was:");
	Console.WriteLine(registerJson);
	return 1;
}

Console.WriteLine($"Paired. username={creds.Value.username}");
Console.WriteLine($"        clientkey={creds.Value.clientKey}");

// 3) Determine channel count from the entertainment configuration (best-effort).
var channelCount = channelsOverride ?? await ResolveChannelCount(http) ?? 16;
Console.WriteLine($"Streaming {channelCount} channels...");

// 4) Open DTLS-PSK and stream animated frames.
var identity = Encoding.UTF8.GetBytes(creds.Value.username);
var psk = Convert.FromHexString(creds.Value.clientKey);

using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
socket.Connect(new IPEndPoint(IPAddress.Parse(host == "localhost" ? "127.0.0.1" : host), 2100));

var dtls = HueDtlsClient.Connect(socket, identity, psk);
Console.WriteLine("DTLS handshake complete. Streaming entertainment frames (Ctrl+C to stop)...");

var entId = await ResolveEntertainmentId(http) ?? Guid.Empty.ToString();
var sw = System.Diagnostics.Stopwatch.StartNew();
var frameIndex = 0;
while (sw.Elapsed.TotalSeconds < seconds) {
	var frame = HueStreamFrame.BuildV2(entId, channelCount, frameIndex);
	dtls.Send(frame, 0, frame.Length);
	frameIndex++;
	await Task.Delay(20); // ~50 Hz
}

Console.WriteLine($"Done. Sent {frameIndex} frames.");
dtls.Close();
return 0;

static async Task<int?> ResolveChannelCount(HttpClient http) {
	try {
		var json = await http.GetStringAsync("/clip/v2/resource/entertainment_configuration");
		return HueRegistration.CountChannels(json);
	} catch {
		return null;
	}
}

static async Task<string?> ResolveEntertainmentId(HttpClient http) {
	try {
		var json = await http.GetStringAsync("/clip/v2/resource/entertainment_configuration");
		return HueRegistration.FirstConfigId(json);
	} catch {
		return null;
	}
}
