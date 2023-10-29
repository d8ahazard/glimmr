"""Support for Glimmr."""
from __future__ import annotations
from glimmr import Glimmr
from homeassistant.components.light import DOMAIN as LIGHT_DOMAIN
from homeassistant.config_entries import ConfigEntry
from homeassistant.const import CONF_HOST, CONF_MAC
from homeassistant.core import HomeAssistant

from .const import DOMAIN, LOGGER

PLATFORMS = {LIGHT_DOMAIN}


async def async_setup(hass: HomeAssistant, config: dict):
    """Old way of setting up the glimmr_light component."""
    hass.data[DOMAIN] = {}
    return True


async def async_setup_entry(hass: HomeAssistant, entry: ConfigEntry):
    """Set up the glimmr_light integration from a config entry."""
    ip_address = entry.data.get(CONF_HOST)
    LOGGER.debug("Creating glimmr from async_setup_entry: %s", ip_address)
    glimmr_dev = Glimmr(ip_address)
    await glimmr_dev.update()
    LOGGER.debug("Updated,using UID of " + entry.unique_id)
    hass.data[DOMAIN][entry.unique_id] = glimmr_dev

    # For backwards compat, set unique ID
    if entry.unique_id is None:
        hass.config_entries.async_update_entry(
            entry, unique_id=entry.data.get(CONF_MAC)
        )

    # Set up all platforms for this device/entry.
    await hass.config_entries.async_forward_entry_setups(entry, PLATFORMS)

    # Reload entry when its updated.
    entry.async_on_unload(entry.add_update_listener(async_reload_entry))

    return True


async def async_unload_entry(hass: HomeAssistant, entry: ConfigEntry) -> bool:
    """Unload Glimmr config entry."""
    unload_ok = await hass.config_entries.async_unload_platforms(entry, PLATFORMS)
    if unload_ok:
        glimmr_dev: Glimmr = hass.data[DOMAIN][entry.entry_id]

        # Ensure disconnected and cleanup stop sub
        await glimmr_dev.socket.stop()
        del hass.data[DOMAIN][entry.entry_id]

    return unload_ok


async def async_reload_entry(hass: HomeAssistant, entry: ConfigEntry) -> None:
    """Reload the config entry when it changed."""
    await hass.config_entries.async_reload(entry.entry_id)
