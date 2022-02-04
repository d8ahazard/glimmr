"""Test the WiZ Light config flow."""
from unittest.mock import patch

import pytest

from homeassistant import config_entries, setup
from homeassistant.components.glimmr_light.config_flow import (
    GlimmrConnectionError,
    GlimmrError
)
from homeassistant.components.glimmr_light.const import DOMAIN
from homeassistant.const import CONF_HOST, CONF_NAME

from tests.common import MockConfigEntry

FAKE_BULB_CONFIG = '{"method":"getSystemConfig","env":"pro","result":\
    { \
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
    "IpAddress": "string" \
    }}'

TEST_SYSTEM_INFO = {"id": "ABCABCABCABC", "name": "Test Bulb"}


TEST_CONNECTION = {CONF_HOST: "1.1.1.1", CONF_NAME: "Test Bulb"}

TEST_NO_IP = {CONF_HOST: "this is no IP input", CONF_NAME: "Test Bulb"}


async def test_form(hass):
    """Test we get the form."""
    await setup.async_setup_component(hass, "persistent_notification", {})
    result = await hass.config_entries.flow.async_init(
        DOMAIN, context={"source": config_entries.SOURCE_USER}
    )
    assert result["type"] == "form"
    assert result["errors"] == {}
    # Patch functions
    with patch(
        "homeassistant.components.glimmr_light.glimmrlight.getBulbConfig",
        return_value=FAKE_BULB_CONFIG,
    ), patch(
        "homeassistant.components.glimmr_light.glimmrlight.getMac",
        return_value="ABCABCABCABC",
    ), patch(
        "homeassistant.components.glimmr_light.async_setup",
        return_value=True,
    ) as mock_setup, patch(
        "homeassistant.components.glimmr_light.async_setup_entry",
        return_value=True,
    ) as mock_setup_entry:
        result2 = await hass.config_entries.flow.async_configure(
            result["flow_id"],
            TEST_CONNECTION,
        )
        await hass.async_block_till_done()

    assert result2["type"] == "create_entry"
    assert result2["title"] == "Test Bulb"
    assert result2["data"] == TEST_CONNECTION
    assert len(mock_setup.mock_calls) == 1
    assert len(mock_setup_entry.mock_calls) == 1


@pytest.mark.parametrize(
    "side_effect, error_base",
    [
        (GlimmrError, "bulb_time_out"),
        (GlimmrConnectionError, "no_wiz_light"),
        (Exception, "unknown"),
        (ConnectionRefusedError, "cannot_connect"),
    ],
)
async def test_user_form_exceptions(hass, side_effect, error_base):
    """Test all user exceptions in the flow."""
    result = await hass.config_entries.flow.async_init(
        DOMAIN, context={"source": config_entries.SOURCE_USER}
    )

    with patch(
        "homeassistant.components.glimmr_light.glimmrlight.getBulbConfig",
        side_effect=side_effect,
    ):
        result2 = await hass.config_entries.flow.async_configure(
            result["flow_id"],
            TEST_CONNECTION,
        )

    assert result2["type"] == "form"
    assert result2["errors"] == {"base": error_base}


async def test_form_updates_unique_id(hass):
    """Test a duplicate id aborts and updates existing entry."""
    entry = MockConfigEntry(
        domain=DOMAIN,
        unique_id=TEST_SYSTEM_INFO["id"],
        data={
            CONF_HOST: "dummy",
            CONF_NAME: TEST_SYSTEM_INFO["name"],
            "id": TEST_SYSTEM_INFO["id"],
        },
    )

    entry.add_to_hass(hass)

    result = await hass.config_entries.flow.async_init(
        DOMAIN, context={"source": config_entries.SOURCE_USER}
    )
    with patch(
        "homeassistant.components.glimmr_light.glimmrlight.getBulbConfig",
        return_value=FAKE_BULB_CONFIG,
    ), patch(
        "homeassistant.components.glimmr_light.async_setup", return_value=True
    ), patch(
        "homeassistant.components.glimmr_light.async_setup_entry",
        return_value=True,
    ):
        result2 = await hass.config_entries.flow.async_configure(
            result["flow_id"],
            TEST_CONNECTION,
        )
        await hass.async_block_till_done()

    assert result2["type"] == "abort"
    assert result2["reason"] == "single_instance_allowed"
