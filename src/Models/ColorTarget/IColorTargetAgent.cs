using Glimmr.Services;
using Q42.HueApi;

namespace Glimmr.Models.ColorTarget {
	/// <summary>
	/// Keeping with our theme of dynamically instantiating classes based on
	/// interface inheritance, we're going to create an interface that devices can use
	/// to create a shared "client" that will be held in ControlService.
	///
	/// It is up to the device implementing ColorTarget to find the agent on instantiation
	/// </summary>
	public interface IColorTargetAgent {
		public dynamic CreateAgent(ControlService cs);
	}
}