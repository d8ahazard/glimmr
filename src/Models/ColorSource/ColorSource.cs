#region

using System.Threading;
using Glimmr.Services;

#endregion

namespace Glimmr.Models.ColorSource {
	public abstract class ColorSource {
		public ColorSource(ColorService cs, ControlService cos, CancellationToken ct) {
		}
	}
}