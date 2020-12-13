﻿#region

using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.Serialization;
using Glimmr.Models.StreamingDevice.Dreamscreen.Encoders;
using Glimmr.Models.Util;
using LiteDB;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.StreamingDevice.Dreamscreen {
	public class DreamData : StreamingData {
		private const string DeviceTag4K = "Dreamscreen4K";
		private static readonly byte[] Required4KEspFirmwareVersion = {1, 6};
		private static readonly byte[] Required4KPicVersionNumber = {5, 6};
		private const string DeviceTagHd = "Dreamscreen";
		private static readonly byte[] RequiredHdEspFirmwareVersion = {1, 6};
		private static readonly byte[] RequiredHdPicVersionNumber = {1, 7};
		private const string DeviceTagSolo = "DreamscreenSolo";
		private static readonly byte[] RequiredSoloEspFirmwareVersion = {1, 6};
		private static readonly byte[] RequiredSoloPicVersionNumber = {6, 2};
		private static readonly byte[] DefaultSectorAssignment = {1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 0, 0, 0};
		

		[JsonProperty] public readonly byte[] EspSerialNumber = {0, 0};

		[DefaultValue(new byte[0])]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public byte[] AppMusicData;

		[DefaultValue(new[] {255, 255, 255})]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int[] MusicModeColors;

		[DefaultValue(new[] {100, 100, 100})]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int[] MusicModeWeights;

		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int AmbientShowType { get; set; }

		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int FadeRate { get; set; }

		[DefaultValue(4)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int ProductId { get; set; }

		[DefaultValue("000000")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string AmbientColor { get; set; }

		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int AmbientModeType { get; set; }

		[DefaultValue(new[]{8, 16, 48, 0, 7, 0})]

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int[] FlexSetup { get; set; }
		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int SkuSetup { get; set; }
		
		[DefaultValue("Undefined       ")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string GroupName { get; set; }
		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int GroupNumber { get; set; }
		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int Mode { get; set; }
		
		[DefaultValue("Dreamscreen4K")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string DeviceTag { get; set; }
		
		[DefaultValue("FFFFFF")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string Saturation { get; set; }
		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int AmbientLightAutoAdjustEnabled { get; set; }

		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int DisplayAnimationEnabled { get; set; }

		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int HdmiInput { get; set; }
		
		
		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool HueLifxSettingsReceived { get; set; }

		
		[DefaultValue(1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int IrEnabled { get; set; }

		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int IrLearningMode { get; set; }

		[DefaultValue(new byte[]{0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0})]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public byte[] IrManifest { get; set; }

		
		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool IsDemo { get; set; }

		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int MicrophoneAudioBroadcastEnabled { get; set; }

		
		[DefaultValue(new byte[]{1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 0, 0, 0})]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public byte[] SectorData { get; set; }

		
		[DefaultValue("")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string ThingName { get; set; }

		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int BootState { get; set; }

		
		[DefaultValue(1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int CecPassthroughEnable { get; set; }

		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int CecPowerEnable { get; set; }

		
		[DefaultValue(1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int CecSwitchingEnable { get; set; }

		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int ColorBoost { get; set; }

		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public byte HdmiActiveChannels { get; set; }

		
		[DefaultValue("HDMI 1          ")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string HdmiInputName1 { get; set; }

		
		[DefaultValue("HDMI 2          ")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string HdmiInputName2 { get; set; }

		
		[DefaultValue("HDMI 3          ")]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public string HdmiInputName3 { get; set; }

		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int HdrToneRemapping { get; set; }

		
		[DefaultValue(1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int HpdEnable { get; set; }

		
		[DefaultValue(1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int IndicatorLightAutoOff { get; set; }

		
		[DefaultValue(1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int LetterboxingEnable { get; set; }

		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int[] MinimumLuminosity { get; set; }

		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int MusicModeSource { get; set; }

		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int MusicModeType { get; set; }
		
		
		[DefaultValue(1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int PillarboxingEnable { get; set; }

		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int SectorBroadcastControl { get; set; }

		
		[DefaultValue(1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int SectorBroadcastTiming { get; set; }

		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int UsbPowerEnable { get; set; }

		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int VideoFrameDelay { get; set; }

		
		[DefaultValue(15)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public byte Zones { get; set; }

		
		[DefaultValue(new[] {255, 255, 255})]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int[] ZonesBrightness { get; set; }
		
		[DefaultValue(new[] {1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 0, 0, 0})]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public byte[] SectorAssignment { get; set; }

		public DreamData() {
			Name = "Dreamscreen4K";
			Tag = "Dreamscreen";
			DeviceTag = "Dreamscreen4K";
		}

		public DreamData(string devTag) {
			Name = "Dreamscreen4K";
			Tag = "Dreamscreen";
			DeviceTag = devTag;
		}

		public void CopyExisting(BsonDocument d) {
			//Reflect our current class to enum keys
			var _type = GetType();
			foreach (var k in d) {
				try {
					var propertyInfo = _type.GetProperty(k.Key);
					if (propertyInfo != null) {
						propertyInfo.SetValue(_type, k.Value, null);	
					}
				} catch (Exception e) {
					LogUtil.Write("Error setting value while copying...");
				}
			}
		}
		
		public void CopyExisting(DreamData d) {
			//Reflect our current class to enum keys
			var _type = GetType();
			var dType = d.GetType();
			foreach (var k in dType.GetProperties()) {
				try {
					var propertyInfo = _type.GetProperty(k.Name);
					if (propertyInfo != null) {
						propertyInfo.SetValue(_type, k.GetValue(_type), null);	
					}
				} catch (Exception e) {
					LogUtil.Write("Error setting value while copying: " + e.Message, "WARN");
				}
			}
		}

		public byte[] EncodeState() {
			switch (DeviceTag) {
				case "Dreamscreen":
					return Encoders.Dreamscreen.EncodeState(this);
				case "Connect":
					return Connect.EncodeState(this);
				case "SideKick":
					return SideKick.EncodeState(this);
				default:
					LogUtil.Write("Invalid device tag? " + DeviceTag);
					return new byte[0];
			}
			
		}
	}
}