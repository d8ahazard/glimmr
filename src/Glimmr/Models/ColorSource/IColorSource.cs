#region

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

#endregion

namespace Glimmr.Models.ColorSource;

public abstract class ColorSource : BackgroundService {
	public abstract bool SourceActive { get; }
	public abstract Task Start(CancellationToken ct);

	public abstract void RefreshSystem();
}