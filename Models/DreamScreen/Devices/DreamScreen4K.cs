﻿using System;
using Newtonsoft.Json.Linq;

namespace Glimmr.Models.DreamScreen.Devices {
    public class DreamScreen4K : DreamScreenHd {
        private const string DeviceTag = "DreamScreen4K";
        private static readonly byte[] Required4KEspFirmwareVersion = {1, 6};
        private static readonly byte[] Required4KPicVersionNumber = {5, 6};

        public DreamScreen4K() {
            SetDefaults();
        }

        public DreamScreen4K(JObject dData) {
            SetDefaults();
            Name = dData.GetValue("Name")?.ToString();
            Id = dData.GetValue("_id")?.ToString();
            
        }

        public sealed override void SetDefaults() {
            base.SetDefaults();
            Initialize();
            ProductId = 2;
            Name = "DreamScreen 4K";
            Tag = DeviceTag;
            EspFirmwareVersion = Required4KEspFirmwareVersion;
            PicVersionNumber = Required4KPicVersionNumber;
            GroupName = "Undefined";
        }

        public DreamScreen4K(string ipAddress) : base(ipAddress) {
            SetDefaults();
            IpAddress = ipAddress;
            Id = ipAddress;
        }
        
        public DreamScreen4K(BaseDevice curDevice) {
            SetDefaults();
            if (curDevice == null) throw new ArgumentException("Invalid baseDevice.");
            Id = curDevice.Id;
            Name = curDevice.Name;
            IpAddress = curDevice.IpAddress;
            Brightness = curDevice.Brightness;
            GroupNumber = curDevice.GroupNumber;
            if (curDevice.flexSetup != null) flexSetup = curDevice.flexSetup;
            Saturation = curDevice.Saturation;
            Mode = curDevice.Mode;
            AmbientColor = curDevice.AmbientColor;
        }
    }
}