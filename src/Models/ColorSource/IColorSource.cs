namespace Glimmr.Models.ColorSource {
	public interface IColorSource {
		bool SourceActive { get; set; }

		public void ToggleStream(bool toggle);
		public void Refresh(SystemData systemData);
	}
}