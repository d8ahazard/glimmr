"""Support for Glimmr."""
from __future__ import annotations

from glimmr import Glimmr
from homeassistant.components.light import DOMAIN as LIGHT_DOMAIN
from homeassistant.config_entries import ConfigEntry
from homeassistant.const import CONF_HOST, CONF_MAC
from homeassistant.core import HomeAssistant

from .const import DOMAIN, LOGGER

PLATFORMS = [LIGHT_DOMAIN]


async def async_setup_entry(hass: HomeAssistant, entry: ConfigEntry) -> bool:
    """Set up the Glimmr integration from a config entry."""
    ip_address = entry.data.get(CONF_HOST)
    LOGGER.debug("Creating glimmr from async_setup_entry: %s", ip_address)
    glimmr_dev = Glimmr(ip_address)
    await glimmr_dev.update()

    # For backwards compat, set unique ID before using it anywhere.
    if entry.unique_id is None:
        hass.config_entries.async_update_entry(
            entry, unique_id=entry.data.get(CONF_MAC)
        )

    LOGGER.debug("Updated, using UID of %s", entry.unique_id)
    hass.data.setdefault(DOMAIN, {})[entry.entry_id] = glimmr_dev

    # Set up all platforms for this device/entry.
    await hass.config_entries.async_forward_entry_setups(entry, PLATFORMS)

    # Reload entry when it's updated.
    entry.async_on_unload(entry.add_update_listener(async_reload_entry))

    return True


async def async_unload_entry(hass: HomeAssistant, entry: ConfigEntry) -> bool:
    """Unload Glimmr config entry."""
    unload_ok = await hass.config_entries.async_unload_platforms(entry, PLATFORMS)
    if unload_ok:
        glimmr_dev: Glimmr = hass.data[DOMAIN].pop(entry.entry_id, None)
        if glimmr_dev is not None:
            # Ensure disconnected and clean up the push subscription.
            try:
                await glimmr_dev.socket.stop()
            except Exception as ex:  # pylint: disable=broad-except
                LOGGER.debug("Error stopping Glimmr socket: %s", ex)

    return unload_ok


async def async_reload_entry(hass: HomeAssistant, entry: ConfigEntry) -> None:
    """Reload the config entry when it changed."""
    await hass.config_entries.async_reload(entry.entry_id)
