using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HueDream.Models.Util;
using Q42.HueApi.Models.Bridge;

namespace HueDream.Models.Hue {
    /// <summary>
    /// Uses M-DNS protocol to locate all Hue Bridge across the network
    /// </summary>
    /// <remarks>https://developers.meethue.com/develop/application-design-guidance/hue-bridge-discovery</remarks>
    public class HueDiscovery {
        private static readonly HttpClient HttpClient = new HttpClient();

        private static readonly Regex XmlResponseCheckHueRegex =
            new Regex(@"Philips hue bridge", RegexOptions.IgnoreCase);

        private static readonly Regex XmlResponseSerialNumberRegex =
            new Regex(@"<serialnumber>(.+?)</serialnumber>", RegexOptions.IgnoreCase);

        private const string HttpXmlDescriptorFileFormat = "http://{0}/description.xml";

        /// <summary>
        /// Multicast group for MDNS Protocol
        /// </summary>
        private readonly IPAddress ssdpMulticastAddress = IPAddress.Parse("224.0.0.251");

        /// <summary>
        /// Multicast port for sending MDNS discovery message
        /// </summary>
        private const int MdnsMulticastPort = 5353;

        /// <summary>
        /// Local port to use to listen to response (same as sending)
        /// </summary>
        private const int MdnsLocalPort = MdnsMulticastPort;

        /// <summary>
        /// MDNS discovery message to send
        /// </summary>
        private readonly byte[] mdnsDiscoveryMessage = BuildMdnsMessage(
            "_hue._tcp.local", // Standard official service name
            "_hap._tcp.local", // Old service name
            "Philips-hue.local" // Old service name
        );

        /// <summary>
        /// Locate bridges
        /// </summary>
        /// <returns>List of bridge IPs found</returns>
        public async Task<IEnumerable<LocatedBridge>> LocateBridges(int timeout = 4) {
            return await LocateBridgesAsync(ssdpMulticastAddress, MdnsMulticastPort, MdnsLocalPort,
                mdnsDiscoveryMessage, timeout).ConfigureAwait(false);
        }

        /// <summary>
        /// Build a MDNS message with a list of queries (= services) to search for
        /// </summary>
        /// <remarks>MDNS specs: https://tools.ietf.org/html/rfc6762 </remarks>
        /// <remarks>DNS specs: https://tools.ietf.org/html/rfc1035 </remarks>
        /// <param name="queries">List of query to search for</param>
        /// <returns>The raw message content (bytes)</returns>
        private static byte[] BuildMdnsMessage(params string[] queries) {
            var bytes = new List<byte>();

            // Build M-DNS Header
            bytes.AddRange(new byte[] {0x00, 0x00}); // Transaction ID = None
            bytes.AddRange(new byte[] {0x01, 0x00}); // Standard query with Recursion
            bytes.AddRange(new byte[] {0x00, (byte) queries.Length}); // Number of Queries
            bytes.AddRange(new byte[] {0x00, 0x00}); // Answer Resource Records = None
            bytes.AddRange(new byte[] {0x00, 0x00}); // Authority Resource Records = None
            bytes.AddRange(new byte[] {0x00, 0x00}); // Additional Resource Records = None

            // Build M-DNS Queries
            foreach (var query in queries) {
                // Each part of the query FQDN is preceded with a byte specifying the length
                // The dot is not actually written
                bytes.AddRange(query.Split('.')
                    .Select(part => Encoding.UTF8.GetBytes(part).ToList())
                    .SelectMany(partBytes => {
                        // Insert the length in front
                        partBytes.Insert(0, (byte) partBytes.Count);
                        return partBytes;
                    }));

                // Add NULL terminator at the end
                bytes.Add(0x00);

                // Add query configuration
                bytes.AddRange(new byte[] {0x00, 0xFF}); // QTYPE = ANY
                bytes.AddRange(new byte[] {0x80, 0xFF}); // UNICAST-RESPONSE + QCLASS = ANY
            }

            return bytes.ToArray();
        }


        private async Task<IEnumerable<LocatedBridge>> LocateBridgesAsync(
            IPAddress multicastAddress, int multicastPort, int localPort, byte[] discoveryMessageContent, int timeout) {
            var discoveredBridges = new ConcurrentDictionary<string, LocatedBridge>();
            var cts = new CancellationTokenSource();
            cts.CancelAfter(timeout * 1000);
            // We will bind to all network interfaces having a private IPv4
            LogUtil.Write("Creating socket.");
            var socket = CreateSocketForMulticastUdpiPv4(new IPEndPoint(IPAddress.Any, localPort), multicastAddress);
            var ep = new IPEndPoint(multicastAddress, multicastPort);
            socket.SendTo(discoveryMessageContent, SocketFlags.None, ep);
            var t = cts.Token;
            await Task.Run(() => ListenSocketAndCheckEveryEndpoint(socket, discoveredBridges, t), cts.Token)
                .ConfigureAwait(false);
            LogUtil.Write("Done?");
            cts.Dispose();
            LogUtil.Write("Closing socket.");
            socket.Close();
            LogUtil.Write("Discover should be done...");
            return discoveredBridges.Select(x => x.Value).ToList();
        }

        /// <summary>
        /// Create a socket for multicast send/receive
        /// </summary>
        /// <param name="localEndpoint">the local endpoint to use</param>
        /// <param name="multicastGroupAddress">the multicast group</param>
        private static Socket
            CreateSocketForMulticastUdpiPv4(IPEndPoint localEndpoint, IPAddress multicastGroupAddress) {
            // Create an IPv4 UDP socket
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            // Allow address reuse
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            // Bind to all interface and to a port (if 0, ask for a free one)
            socket.Bind(localEndpoint);

            // Set TTL to 1: it will stays on the local network
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);

            // Join Multicast group
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                new MulticastOption(multicastGroupAddress, localEndpoint.Address));

            return socket;
        }

        /// <summary>
        /// Listen to any response on a socket and check if responding IP is a Hue Bridge
        /// </summary>
        /// <param name="socket">The socket to listen to</param>
        /// <param name="discoveredBridges">The dictionary to fill with located bridges</param>
        private static void ListenSocketAndCheckEveryEndpoint(Socket socket,
            ConcurrentDictionary<string, LocatedBridge> discoveredBridges, CancellationToken ct) {
            try {
                var ipSeen = new List<string>();
                var socketAddress = ((IPEndPoint) socket.LocalEndPoint).Address;

                while (!ct.IsCancellationRequested) {
                    var responseRawBuffer = new byte[8000];
                    EndPoint responseEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    socket.ReceiveFrom(responseRawBuffer, ref responseEndPoint);
                    try {
                        var responseIpAddress = ((IPEndPoint) responseEndPoint).Address;
                        if (socketAddress.Equals(responseIpAddress) || ipSeen.Contains(responseIpAddress.ToString()))
                            continue;
                        var responseBody = Encoding.UTF8.GetString(responseRawBuffer);
                        if (string.IsNullOrWhiteSpace(responseBody)) continue;

                        // Spin up a new thread to handle this specific response so we can continue waiting for response
                        Task.Run(() => {
                            try {
                                // Check if it's a Hue Bridge
                                var serialNumber = CheckHueDescriptor(responseIpAddress,
                                    TimeSpan.FromMilliseconds(1000)).Result;

                                if (string.IsNullOrEmpty(serialNumber)) return;
                                LogUtil.Write($"Adding discovered bridge: {responseIpAddress}, {serialNumber}");
                                discoveredBridges.TryAdd(responseIpAddress.ToString(), new LocatedBridge {
                                    IpAddress = responseIpAddress.ToString(),
                                    BridgeId = serialNumber
                                });
                            } catch (Exception e) {
                                LogUtil.Write($"We have an issue with discovery: {e.Message}", "WARN");
                            }
                        }, ct);
                        ipSeen.Add(responseIpAddress.ToString());
                    } catch (Exception e) {
                        LogUtil.Write("Parsing exception: " + e.Message, "WARN");
                    }
                }
            } catch (Exception e) {
                LogUtil.Write("Socket exception: " + e.Message, "WARN");
            }
        }

        private static async Task<string> CheckHueDescriptor(IPAddress ip, TimeSpan httpTimeout,
            CancellationToken? cancellationToken = null) {
            using var httpTimeoutCts = new CancellationTokenSource(httpTimeout);
            using var mergedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken ?? CancellationToken.None, httpTimeoutCts.Token);
            try {
                var uri = new Uri(string.Format(CultureInfo.InvariantCulture, HttpXmlDescriptorFileFormat, ip));
                using var response = await HttpClient.GetAsync(uri, mergedCts.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode) {
                    var xmlResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (XmlResponseCheckHueRegex.IsMatch(xmlResponse)) {
                        var serialNumberMatch = XmlResponseSerialNumberRegex.Match(xmlResponse);

                        if (serialNumberMatch.Success) {
                            var serial = serialNumberMatch.Groups[1].Value;
                            LogUtil.Write("Serial1match: " + serial);
                            return serial;
                        }
                    }
                }
            } catch (Exception e) {
            }

            return "";
        }
    }
}