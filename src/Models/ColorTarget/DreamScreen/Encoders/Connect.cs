﻿using System;
using System.Collections.Generic;
using Glimmr.Models.Util;

namespace Glimmr.Models.ColorTarget.DreamScreen.Encoders {
	public static class Connect {

		public static DreamData ParseePayload(byte[] payload) {
			var dd = new DreamData {DeviceTag = "Connect"};
			if (payload != null) {
				try {
					var name = ByteUtils.ExtractString(payload, 0, 16);
					if (name.Length == 0) name = "Connect";
					dd.Name = name;
					var groupName = ByteUtils.ExtractString(payload, 16, 32);
					if (groupName.Length == 0) groupName = "Group";
					dd.GroupName = groupName;
				}
				catch (IndexOutOfRangeException) {
					Console.WriteLine($@"Index out of range, payload length is {payload.Length}.");
				}

				dd.DeviceGroup = payload[32];
				dd.DeviceMode = payload[33];
				dd.Brightness = payload[34];
				dd.AmbientColor = ByteUtils.ExtractString(payload, 35, 38, true);
				dd.Saturation = ByteUtils.ExtractString(payload, 38, 41, true);
				dd.FadeRate = payload[41];
				dd.AmbientMode = payload[59];
				dd.AmbientShowType = payload[60];
				dd.HdmiInput = payload[61];
				dd.DisplayAnimationEnabled = payload[62];
				dd.AmbientLightAutoAdjustEnabled = payload[63];
				dd.MicrophoneAudioBroadcastEnabled = payload[64];
				dd.IrEnabled = payload[65];
				dd.IrLearningMode = payload[66];
				dd.IrManifest = ByteUtils.ByteInts(ByteUtils.ExtractBytes(payload, 67, 107));
				if (payload.Length > 115)
					try {
						dd.ThingName = ByteUtils.ExtractString(payload, 115, 178);
					}
					catch (IndexOutOfRangeException) {
						dd.ThingName = "";
					}
			}

			return dd;
		}
        
        

		public static byte[] EncodeState(DreamData dd) {
			var response = new List<byte>();
			response.AddRange(ByteUtils.StringBytePad(dd.Name, 16));
			response.AddRange(ByteUtils.StringBytePad(dd.GroupName, 16));
			response.Add(ByteUtils.IntByte(dd.DeviceGroup));
			response.Add(ByteUtils.IntByte(dd.DeviceMode));
			response.Add(ByteUtils.IntByte(dd.Brightness));
			response.AddRange(ByteUtils.StringBytes(dd.AmbientColor));
			response.AddRange(ByteUtils.StringBytes(dd.Saturation));
			response.Add(ByteUtils.IntByte(dd.FadeRate));
			response.Add(ByteUtils.IntByte(dd.AmbientMode));
			response.Add(ByteUtils.IntByte(dd.AmbientShowType));
			response.Add(ByteUtils.IntByte(dd.HdmiInput));
			response.Add(ByteUtils.IntByte(dd.DisplayAnimationEnabled));
			response.Add(ByteUtils.IntByte(dd.AmbientLightAutoAdjustEnabled));
			response.Add(ByteUtils.IntByte(dd.MicrophoneAudioBroadcastEnabled));
			response.Add(ByteUtils.IntByte(dd.IrEnabled));
			response.Add(ByteUtils.IntByte(dd.IrLearningMode));
			response.AddRange(ByteUtils.IntBytes(dd.IrManifest));
			response.AddRange(ByteUtils.StringBytePad(dd.ThingName, 63));
			// Type
			response.Add(0x04);
			return response.ToArray();
		}
	}
}