namespace Glimmr.Models.ColorSource {
	public interface IColorSource {
		public bool Streaming { get; set; }
		public abstract void ToggleSend(bool enable = false);
		public abstract void Initialize();
		public abstract void Refresh();
	}
}