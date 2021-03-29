using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;

namespace Glimmr.Models.ColorTarget.DreamScreen {
	public class DreamScreenDiscovery : ColorDiscovery, IColorDiscovery {

		public override string DeviceTag { get; set; } = "DreamScreen";
		private UdpClient _listener;
		private readonly DreamUtil _dreamUtil;
		private readonly IPEndPoint _broadcastAddress = new(IPAddress.Parse("255.255.255.255"), 8888);
		private readonly IPEndPoint _listenEndPoint = new(IPAddress.Any, 8888);
		private readonly ControlService _controlService;
		private bool _discovering;
		
		public DreamScreenDiscovery(ColorService cs) : base(cs) {
			_controlService = cs.ControlService;
			_dreamUtil = new DreamUtil(_controlService.UdpClient);
		}
		public async Task Discover(CancellationToken ct) {
			Log.Debug("Dreamscreen: Discovery started...");
			// Send our notification to actually discover
			_discovering = true;
			_listener = new UdpClient();
			_listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			_listener.EnableBroadcast = true;
			_listener.Client.Bind(_listenEndPoint);
			
			var msg = new byte[] {0xFC, 0x05, 0xFF, 0x30, 0x01, 0x0A, 0x2A};
			await _dreamUtil.SendUdpMessage(msg, _broadcastAddress);
			while (!ct.IsCancellationRequested) {
				//IPEndPoint object will allow us to read datagrams sent from any source.
				var receivedResults = await _listener.ReceiveAsync();
				await ParseMessage(receivedResults.Buffer, receivedResults.RemoteEndPoint).ConfigureAwait(false);
			}
			
			_discovering = false;
			_listener.Dispose();
			Log.Debug("Dreamscreen: Discovery complete.");
		}
		
		private async Task ParseMessage(byte[] data, IPEndPoint sender) {
			if (!MsgUtils.CheckCrc(data)) {
					Log.Debug("INVALID CRC");
					return;
			}
			var from = sender.Address.ToString();
			var msg = new DreamscreenMessage(data, from);
			var target = new IPEndPoint(sender.Address, 8888);
			Log.Debug("Parsing message: " + msg);

			var dev = msg.Device;
			if (msg.Command == "DEVICE_DISCOVERY") {
				if (msg.Flags == "60" && from != IpUtil.GetLocalIpAddress()) {
					Log.Debug("Adding device!");
					if (dev != null) {
						dev.IpAddress = from;
						await _controlService.AddDevice(dev);
						await _dreamUtil.SendUdpWrite(0x01, 0x03, new byte[]{0},0x60,0, target);
                            
					} else {
						Log.Warning("Message device is null!.");
					}
				}
			}
		}
	}
}