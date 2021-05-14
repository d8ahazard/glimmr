namespace Glimmr.Models.ColorSource {
	public interface IColorSource {

		public void ToggleStream(bool toggle);
		public void Refresh(SystemData systemData);

		bool SourceActive { get; set; }
	}
}