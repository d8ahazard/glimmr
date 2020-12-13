#region

using System;
using System.Collections.Generic;
using Glimmr.Models.Util;
using Serilog;

#endregion

namespace Glimmr.Models.StreamingDevice.Dreamscreen.Encoders {
	public static class SideKick {
		public static DreamData ParsePayload(byte[] payload) {
			var dd = new DreamData();
			if (payload is null) throw new ArgumentNullException(nameof(payload));
			var name = ByteUtils.ExtractString(payload, 0, 16);
			if (name.Length == 0) name = dd.DeviceTag;
			dd.Name = name;
			var groupName = ByteUtils.ExtractString(payload, 16, 32);
			if (groupName.Length == 0) groupName = "unassigned";
			dd.GroupName = groupName;
			dd.GroupNumber = payload[32];
			dd.Mode = payload[33];
			dd.Brightness = payload[34];
			dd.AmbientColor = ByteUtils.ExtractString(payload, 35, 38, true);
			dd.Saturation = ByteUtils.ExtractString(payload, 38, 41, true);
			dd.FadeRate = payload[41];
			dd.SectorAssignment = ByteUtils.ExtractBytes(payload, 42, 57);
			if (payload.Length == 62) {
				dd.AmbientModeType = payload[59];
				dd.AmbientShowType = payload[60];
			}

			return dd;
		}

		public static byte[] EncodeState(DreamData dd) {
			Log.Debug("Encoding sidekick state.");
			var response = new List<byte>();
			response.AddRange(ByteUtils.StringBytePad(dd.Name, 16));
			response.AddRange(ByteUtils.StringBytePad(dd.GroupName, 16));
			response.Add(ByteUtils.IntByte(dd.GroupNumber));
			response.Add(ByteUtils.IntByte(dd.Mode));
			response.Add(ByteUtils.IntByte(dd.Brightness));
			response.AddRange(ByteUtils.StringBytes(dd.AmbientColor));
			response.AddRange(ByteUtils.StringBytes(dd.Saturation));
			response.Add(ByteUtils.IntByte(dd.FadeRate));
			// Sector Data
			response.AddRange(new byte[15]);
			response.Add(ByteUtils.IntByte(dd.AmbientModeType));
			response.Add(ByteUtils.IntByte(dd.AmbientShowType));
			// Type
			response.Add(0x03);
			return response.ToArray();
		}
	}
}