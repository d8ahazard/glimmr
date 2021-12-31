#region

using System.Threading.Tasks;

#endregion

namespace Glimmr.Models.ColorTarget;

public interface IColorTargetAuth {
	public Task<dynamic> CheckAuthAsync(dynamic deviceData);
}