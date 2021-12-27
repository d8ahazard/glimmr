#region

using System;
using DreamScreenNet;
using Glimmr.Services;

#endregion

namespace Glimmr.Models.ColorTarget.DreamScreen; 

public class DreamAgent : IColorTargetAgent {
	private DreamScreenClient? _du;

	public dynamic? CreateAgent(ControlService cs) {
		_du = new DreamScreenClient(cs.UdpClient);
		return _du;
	}

	public void Dispose() {
		_du?.Dispose();
		GC.SuppressFinalize(this);
	}
}