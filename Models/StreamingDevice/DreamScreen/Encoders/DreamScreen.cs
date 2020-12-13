using System;
using System.Collections.Generic;
using Glimmr.Models.Util;

namespace Glimmr.Models.StreamingDevice.Dreamscreen.Encoders {
	public static class Dreamscreen {
		
		private const string DeviceTag4K = "Dreamscreen4K";
		private static readonly byte[] Required4KEspFirmwareVersion = {1, 6};
		private static readonly byte[] Required4KPicVersionNumber = {5, 6};
		private const string DeviceTagHd = "Dreamscreen";
		private static readonly byte[] RequiredHdEspFirmwareVersion = {1, 6};
		private static readonly byte[] RequiredHdPicVersionNumber = {1, 7};
		private const string DeviceTagSolo = "DreamscreenSolo";
		private static readonly byte[] RequiredSoloEspFirmwareVersion = {1, 6};
		private static readonly byte[] RequiredSoloPicVersionNumber = {6, 2};
		public static readonly byte[] DefaultSectorAssignment = {1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 0, 0, 0};

		
		public static DreamData ParsePayload(byte[] payload) {
			var dd = new DreamData();
			if (payload is null) throw new ArgumentNullException(nameof(payload));
			if (payload.Length < 132)
				throw new ArgumentException($"Payload length is too short: {ByteUtils.ByteString(payload)}");
			var name1 = ByteUtils.ExtractString(payload, 0, 16);
			if (name1.Length == 0) name1 = dd.DeviceTag;
			dd.Name = name1;
			var groupName1 = ByteUtils.ExtractString(payload, 16, 32);
			if (groupName1.Length == 0) groupName1 = "Group";
			dd.GroupName = groupName1;
			dd.GroupNumber = payload[32];
			dd.Mode = payload[33];
			dd.Brightness = payload[34];
			dd.Zones = payload[35];
			dd.ZonesBrightness = ByteUtils.ExtractInt(payload, 36, 40);
			dd.AmbientColor = ByteUtils.ExtractString(payload, 40, 43, true);
			dd.Saturation = ByteUtils.ExtractString(payload, 43, 46, true);
			dd.FlexSetup = ByteUtils.ExtractInt(payload, 46, 52);
			dd.MusicModeType = payload[52];
			dd.MusicModeColors = ByteUtils.ExtractInt(payload, 53, 56);
			dd.MusicModeWeights = ByteUtils.ExtractInt(payload, 56, 59);
			dd.MinimumLuminosity = ByteUtils.ExtractInt(payload, 59, 62);
			dd.AmbientShowType = payload[62];
			dd.FadeRate = payload[63];
			dd.IndicatorLightAutoOff = payload[69];
			dd.UsbPowerEnable = payload[70];
			dd.SectorBroadcastControl = payload[71];
			dd.SectorBroadcastTiming = payload[72];
			dd.HdmiInput = payload[73];
			dd.MusicModeSource = payload[74];
			dd.HdmiInputName1 = ByteUtils.ExtractString(payload, 75, 91);
			dd.HdmiInputName2 = ByteUtils.ExtractString(payload, 91, 107);
			dd.HdmiInputName3 = ByteUtils.ExtractString(payload, 107, 123);
			dd.CecPassthroughEnable = payload[123];
			dd.CecSwitchingEnable = payload[124];
			dd.HpdEnable = payload[125];
			dd.VideoFrameDelay = payload[127];
			dd.LetterboxingEnable = payload[128];
			dd.HdmiActiveChannels = payload[129];
			dd.ColorBoost = payload[134];
			if (payload.Length >= 137) dd.CecPowerEnable = payload[135];
			if (payload.Length >= 138) dd.SkuSetup = payload[136];
			if (payload.Length >= 139) dd.BootState = payload[137];
			if (payload.Length >= 140) dd.PillarboxingEnable = payload[138];
			if (payload.Length >= 141) dd.HdrToneRemapping = payload[139];
			return dd;
		}

		public static byte[] EncodeState(DreamData dd) {
			var espVersion = RequiredHdEspFirmwareVersion;
			var picVersion = RequiredHdPicVersionNumber;
			if (dd.DeviceTag == DeviceTag4K) {
				espVersion = Required4KEspFirmwareVersion;
				picVersion = Required4KPicVersionNumber;
			}

			if (dd.DeviceTag == DeviceTagSolo) {
				espVersion = RequiredSoloEspFirmwareVersion;
				picVersion = RequiredSoloPicVersionNumber;
			}
			LogUtil.Write("Encoding state for DS.");
			var response = new List<byte>();
			var nByte = ByteUtils.StringBytePad(dd.Name, 16);
			response.AddRange(nByte);
			var gByte = ByteUtils.StringBytePad(dd.GroupName, 16);
			response.AddRange(gByte);
			response.Add(ByteUtils.IntByte(dd.GroupNumber));
			response.Add(ByteUtils.IntByte(dd.Mode));
			response.Add(ByteUtils.IntByte(dd.Brightness));
			response.Add(dd.Zones);
			response.AddRange(ByteUtils.IntBytes(dd.ZonesBrightness));
			response.AddRange(ByteUtils.StringBytes(dd.AmbientColor));
			response.AddRange(ByteUtils.StringBytes(dd.Saturation));
			response.AddRange(ByteUtils.IntBytes(dd.FlexSetup));
			response.Add(ByteUtils.IntByte(dd.MusicModeType));
			response.AddRange(ByteUtils.IntBytes(dd.MusicModeColors));
			response.AddRange(ByteUtils.IntBytes(dd.MusicModeWeights));
			response.AddRange(ByteUtils.IntBytes(dd.MinimumLuminosity));
			response.Add(ByteUtils.IntByte(dd.AmbientShowType));
			response.Add(ByteUtils.IntByte(dd.FadeRate));
			response.AddRange(new byte[5]);
			response.Add(ByteUtils.IntByte(dd.IndicatorLightAutoOff));
			response.Add(ByteUtils.IntByte(dd.UsbPowerEnable));
			response.Add(ByteUtils.IntByte(dd.SectorBroadcastControl));
			response.Add(ByteUtils.IntByte(dd.SectorBroadcastTiming));
			response.Add(ByteUtils.IntByte(dd.HdmiInput));
			response.AddRange(new byte[2]);
			string[] iList = {dd.HdmiInputName1, dd.HdmiInputName2, dd.HdmiInputName3};
			foreach (var iName in iList) response.AddRange(ByteUtils.StringBytePad(iName, 16));
			response.Add(ByteUtils.IntByte(dd.CecPassthroughEnable));
			response.Add(ByteUtils.IntByte(dd.CecSwitchingEnable));
			response.Add(ByteUtils.IntByte(dd.HpdEnable));
			response.Add(0x00);
			response.Add(ByteUtils.IntByte(dd.VideoFrameDelay));
			response.Add(ByteUtils.IntByte(dd.LetterboxingEnable));
			response.Add(ByteUtils.IntByte(dd.HdmiActiveChannels));
			response.AddRange(espVersion);
			response.AddRange(picVersion);
			response.Add(ByteUtils.IntByte(dd.ColorBoost));
			response.Add(ByteUtils.IntByte(dd.CecPowerEnable));
			response.Add(ByteUtils.IntByte(dd.SkuSetup));
			response.Add(ByteUtils.IntByte(dd.BootState));
			response.Add(ByteUtils.IntByte(dd.PillarboxingEnable));
			response.Add(ByteUtils.IntByte(dd.HdrToneRemapping));
			// Device type
			if (dd.DeviceTag == "Dreamscreen")
				response.Add(0x01);
			else if (dd.DeviceTag == "Dreamscreen4K")
				response.Add(0x02);
			else
				//DS Solo
				response.Add(0x07);

			return response.ToArray();
		}
	}
}