"""Tests for the WiZ Light integration."""

import json

from homeassistant.components.glimmr_light.const import DOMAIN
from homeassistant.const import CONF_IP_ADDRESS, CONF_NAME
from homeassistant.helpers.typing import HomeAssistantType

from tests.common import MockConfigEntry

FAKE_BULB_CONFIG = json.loads(
    '{"method":"getSystemConfig","env":"pro","result":\
    {\
    "AutoDisabled": false,\
    "AutoRemoveDevices": false,\
    "AutoUpdate": false,\
    "CropBlackLevel": 7,\
    "DefaultSet": false,\
    "EnableAutoBrightness": true,\
    "EnableAutoDisable": true,\
    "EnableLetterBox": true,\
    "EnablePillarBox": true,\
    "SkipDemo": false,\
    "SkipTour": false,\
    "UseCenter": false,\
    "AblAmps": 3,\
    "AblVolts": 5,\
    "AudioGain": 0.5,\
    "AudioMin": 0.025,\
    "AmbientScene": 0,\
    "AudioScene": 0,\
    "AutoDisableDelay": 30,\
    "AutoDiscoveryFrequency": 60,\
    "AutoRemoveDevicesAfter": 7,\
    "AutoUpdateTime": 2,\
    "BaudRate": 115200,\
    "BottomCount": 96,\
    "CamType": 0,\
    "CaptureMode": 1,\
    "CropDelay": 15,\
    "DeviceMode": 0,\
    "DiscoveryTimeout": 10,\
    "HSectors": 10,\
    "LedCount": 0,\
    "LeftCount": 54,\
    "OpenRgbPort": 6742,\
    "PreviewMode": 0,\
    "PreviousMode": 0,\
    "RightCount": 54,\
    "SectorCount": 0,\
    "StreamMode": 0,\
    "TopCount": 96,\
    "UsbSelection": 0,\
    "VSectors": 6,\
    "AmbientColor": "string",\
    "DeviceName": "",\
    "DsIp": "",\
    "OpenRgbIp": "127.0.0.1",\
    "RecDev": "",\
    "Theme": "dark",\
    "TimeZone": "US/Central",\
    "Units": 0,\
    "BlackLevel": 7,\
    "Version": "string",\
    "IpAddress": "string"}}'
)

REAL_BULB_CONFIG = json.loads(
    '{"method":"getSystemConfig","env":"pro","result":\
    {\
    "AutoDisabled": false,\
    "AutoRemoveDevices": false,\
    "AutoUpdate": false,\
    "CropBlackLevel": 7,\
    "DefaultSet": false,\
    "EnableAutoBrightness": true,\
    "EnableAutoDisable": true,\
    "EnableLetterBox": true,\
    "EnablePillarBox": true,\
    "SkipDemo": false,\
    "SkipTour": false,\
    "UseCenter": false,\
    "AblAmps": 3,\
    "AblVolts": 5,\
    "AudioGain": 0.5,\
    "AudioMin": 0.025,\
    "AmbientScene": 0,\
    "AudioScene": 0,\
    "AutoDisableDelay": 30,\
    "AutoDiscoveryFrequency": 60,\
    "AutoRemoveDevicesAfter": 7,\
    "AutoUpdateTime": 2,\
    "BaudRate": 115200,\
    "BottomCount": 96,\
    "CamType": 0,\
    "CaptureMode": 1,\
    "CropDelay": 15,\
    "DeviceMode": 0,\
    "DiscoveryTimeout": 10,\
    "HSectors": 10,\
    "LedCount": 0,\
    "LeftCount": 54,\
    "OpenRgbPort": 6742,\
    "PreviewMode": 0,\
    "PreviousMode": 0,\
    "RightCount": 54,\
    "SectorCount": 0,\
    "StreamMode": 0,\
    "TopCount": 96,\
    "UsbSelection": 0,\
    "VSectors": 6,\
    "AmbientColor": "string",\
    "DeviceName": "",\
    "DsIp": "",\
    "OpenRgbIp": "127.0.0.1",\
    "RecDev": "",\
    "Theme": "dark",\
    "TimeZone": "US/Central",\
    "Units": 0,\
    "BlackLevel": 7,\
    "Version": "string",\
    "IpAddress": "string"}}'
)

TEST_SYSTEM_INFO = {"id": "ABCABCABCABC", "name": "Test Glimmr"}

TEST_CONNECTION = {CONF_IP_ADDRESS: "1.1.1.1", CONF_NAME: "Test Glimmr"}


async def setup_integration(
    hass: HomeAssistantType,
) -> MockConfigEntry:
    """Mock ConfigEntry in Home Assistant."""

    entry = MockConfigEntry(
        domain=DOMAIN,
        unique_id=TEST_SYSTEM_INFO["id"],
        data={
            CONF_IP_ADDRESS: "127.0.0.1",
            CONF_NAME: TEST_SYSTEM_INFO["name"],
        },
    )

    entry.add_to_hass(hass)

    await hass.config_entries.async_setup(entry.entry_id)
    await hass.async_block_till_done()

    return entry
