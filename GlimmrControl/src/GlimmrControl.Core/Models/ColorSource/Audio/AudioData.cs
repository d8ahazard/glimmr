namespace GlimmrControl.Core.Models.ColorSource.Audio {
	public class AudioData {
		public bool IsDefault { get; set; }
		public bool IsEnabled { get; set; }
		public bool IsInitialized { get; set; }
		public bool IsLoopback { get; set; }
		public DeviceType Type { get; set; }
		public string Driver { get; set; }
		public string Id { get; set; }
		public string Name { get; set; }

		public enum DeviceType {
			/// <summary>
			///     An audio endpoint Device that the User accesses remotely through a network.
			/// </summary>
			Network = 16777216, // 0x01000000

			/// <summary>A set of speakers.</summary>
			Speakers = 33554432, // 0x02000000

			/// <summary>
			///     An audio endpoint Device that sends a line-level analog signal to
			///     a line-Input jack on an audio adapter or that receives a line-level analog signal
			///     from a line-output jack on the adapter.
			/// </summary>
			Line = 50331648, // 0x03000000

			/// <summary>A set of headphones.</summary>
			Headphones = 67108864, // 0x04000000

			/// <summary>A microphone.</summary>
			Microphone = 83886080, // 0x05000000

			/// <summary>
			///     An earphone or a pair of earphones with an attached mouthpiece for two-way communication.
			/// </summary>
			Headset = 100663296, // 0x06000000

			/// <summary>
			///     The part of a telephone that is held in the hand and
			///     that contains a speaker and a microphone for two-way communication.
			/// </summary>
			Handset = 117440512, // 0x07000000

			/// <summary>
			///     An audio endpoint Device that connects to an audio adapter through a connector
			///     for a digital interface of unknown Type.
			/// </summary>
			Digital = 134217728, // 0x08000000

			/// <summary>
			///     An audio endpoint Device that connects to an audio adapter through
			///     a Sony/Philips Digital Interface (S/PDIF) connector.
			/// </summary>
			SPDIF = 150994944, // 0x09000000

			/// <summary>
			///     An audio endpoint Device that connects to an audio adapter through
			///     a High-Definition Multimedia Interface (HDMI) connector.
			/// </summary>
			HDMI = 167772160, // 0x0A000000

			/// <summary>
			///     An audio endpoint Device that connects to an audio adapter through a DisplayPort connector.
			/// </summary>
			DisplayPort = 1073741824 // 0x40000000
		}
	}
}