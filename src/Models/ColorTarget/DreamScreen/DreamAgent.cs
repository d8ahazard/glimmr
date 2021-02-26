using System.Net.Sockets;
using Glimmr.Models.Util;
using Glimmr.Services;

namespace Glimmr.Models.ColorTarget.DreamScreen {
	public class DreamAgent : IColorTargetAgent {
		
		public dynamic CreateAgent(ControlService cs) {
			return new DreamUtil(cs.UdpClient);
		}
	}
}