#region

using System;
using Glimmr.Services;

#endregion

namespace Glimmr.Models.ColorTarget.Govee;

public class GoveeAgent : IColorTargetAgent {
	public dynamic? CreateAgent(ControlService cs) {
		return null;
	}

	public void Dispose() {
		GC.SuppressFinalize(this);
	}
}
