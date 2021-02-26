using System;

namespace Glimmr.Models.ColorTarget.Corsair {
	public class CorsairData : IColorTargetData {
		public string Name { get; set; }
		public string Id { get; set; }
		public string Tag { get; set; }
		public string IpAddress { get; set; }
		public int Brightness { get; set; }
		public bool Enable { get; set; }
		public string LastSeen { get; set; }
		public int Offset { get; set; }
		public int Reverse { get; set; }

		public void CopyExisting(IColorTargetData data) {
			CorsairData cd = (CorsairData) data;
			Id = cd.Id;
			Brightness = cd.Brightness;
			Enable = cd.Enable;
			Offset = cd.Offset;
			Reverse = cd.Reverse;
		}
	}
}