using System.Collections.Generic;
using System.Drawing;
using System.Threading;

namespace Glimmr.Models.ColorSource {
	public interface IColorSource {
		public abstract void Initialize(CancellationToken ct);
		public abstract void Refresh();
		
		public List<Color> Colors { get; }
		public List<Color> Sectors { get; }
	}
}