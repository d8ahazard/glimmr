#region

using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;

#endregion

namespace GlimmrControl.Core {
	internal static class NetUtility {
		//we just assume we are connected to embedded AP if:
		//1. the IP is in 10.41.0.0/24 subnet
		//2. the device IP is between 2 and 5 (ESP8266 DHCP range) 
		public static bool IsConnectedToGlimmrAp() {
			return NetworkInterface.GetAllNetworkInterfaces().Where(netInterface => netInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || netInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet).Any(netInterface => (from addressInfo in netInterface.GetIPProperties().UnicastAddresses where addressInfo.Address.AddressFamily == AddressFamily.InterNetwork select addressInfo.Address into ip select ip.ToString()).Any(ips => ips.StartsWith("10.41.0.")));
		}
	}
}