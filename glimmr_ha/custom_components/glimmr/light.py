"""Glimmr light platform."""
from __future__ import annotations

import asyncio
from typing import Any

from glimmr import Glimmr
from glimmr.exceptions import GlimmrError
from homeassistant.components.light import (
    ATTR_EFFECT,
    ATTR_RGB_COLOR,
    ColorMode,
    LightEntity,
    LightEntityFeature,
)
from homeassistant.config_entries import ConfigEntry
from homeassistant.core import HomeAssistant
from homeassistant.helpers.entity_platform import AddEntitiesCallback

from .const import DOMAIN, LOGGER

# Pseudo-effects exposing Glimmr device modes through the effect list.
# Values match Glimmr's DeviceMode enum.
MODE_EFFECTS: dict[str, int] = {
    "Mode: Video": 1,
    "Mode: Audio": 2,
    "Mode: Ambient": 3,
    "Mode: Audio/Video": 4,
    "Mode: Streaming": 5,
    "Mode: DreamScreen": 6,
    "Mode: Hue Sync": 7,
}

# Legacy negative scene IDs (from the glimmr python library) to device modes.
LEGACY_SCENE_MODES: dict[int, int] = {
    -2: 1,  # Video
    -3: 2,  # Audio
    -4: 3,  # Ambient
    -5: 4,  # Audio/Video
    -6: 5,  # Streaming
    -7: 6,  # DreamScreen
    -8: 7,  # Hue Sync
}


async def async_setup_entry(
    hass: HomeAssistant,
    entry: ConfigEntry,
    async_add_entities: AddEntitiesCallback,
) -> None:
    """Set up the Glimmr light from a config entry."""
    glimmr: Glimmr = hass.data[DOMAIN][entry.entry_id]
    LOGGER.debug("Setting up glimmr light for entry %s", entry.entry_id)
    glimmr_light = GlimmrLight(glimmr)
    async_add_entities([glimmr_light], update_before_add=True)

    async def async_update(call=None) -> None:
        """Trigger a state refresh (matches the `update` service in services.yaml)."""
        LOGGER.debug("[glimmr light] update requested")
        await glimmr_light.async_update(force=True)
        glimmr_light.async_write_ha_state()

    hass.services.async_register(DOMAIN, "update", async_update)


class GlimmrLight(LightEntity):
    """Representation of a Glimmr device."""

    _attr_icon = "mdi:led-strip-variant"
    _attr_color_mode = ColorMode.RGB
    _attr_supported_color_modes = {ColorMode.RGB}
    _attr_supported_features = LightEntityFeature.EFFECT

    def __init__(self, glimmr: Glimmr) -> None:
        """Initialize a Glimmr light."""
        LOGGER.debug("Initializing light...")
        self.glimmr: Glimmr = glimmr
        self.glimmr.LOGGER = LOGGER
        self._state = self.glimmr.system_data.device_mode
        if self.glimmr.system_data.auto_disabled:
            self._state = 0
        self._attr_brightness = 255
        self._attr_name = self.glimmr.system_data.device_name
        self._attr_unique_id = str(self.glimmr.system_data.device_id)
        self._rgb_color: tuple[int, int, int] = (255, 255, 255)
        self._available: bool | None = None
        self._effect: str | None = None
        self._scenes: list[str] = []
        self.update_color()
        self.update_scene_list_sync()

    # ------------------------------------------------------------------ #
    # Lifecycle
    # ------------------------------------------------------------------ #

    async def async_added_to_hass(self) -> None:
        """Register device notification."""
        LOGGER.debug("Added, connecting to ws.")
        await self.async_initialize_device()

    async def async_will_remove_from_hass(self) -> None:
        if self.glimmr.connected:
            LOGGER.debug("Disconnecting from ws.")
            await self.glimmr.socket.stop()

    # ------------------------------------------------------------------ #
    # Properties
    # ------------------------------------------------------------------ #

    @property
    def rgb_color(self) -> tuple[int, int, int]:
        """Return the ambient color."""
        return self._rgb_color

    @property
    def is_on(self) -> bool:
        """Return true if the device is in any active mode."""
        return self._state != 0

    @property
    def should_poll(self) -> bool:
        """Poll only while the push socket is down."""
        return self.glimmr.connected is False

    @property
    def effect(self) -> str | None:
        """Return the current effect."""
        return self._effect

    @property
    def effect_list(self) -> list[str]:
        """Ambient scenes plus device-mode pseudo effects."""
        return [*self._scenes, *MODE_EFFECTS.keys()]

    @property
    def available(self) -> bool | None:
        """Return if light is available."""
        return self._available

    @property
    def device_info(self):
        """Get device specific attributes."""
        return {
            "identifiers": {(DOMAIN, str(self.glimmr.system_data.device_id))},
            "name": self._attr_name,
            "manufacturer": "D8ahazard",
            "model": "Glimmr",
            "sw_version": getattr(self.glimmr.system_data, "version", None),
        }

    # ------------------------------------------------------------------ #
    # Commands
    # ------------------------------------------------------------------ #

    async def async_turn_on(self, **kwargs: Any) -> None:
        """Instruct the light to turn on."""
        if ATTR_RGB_COLOR in kwargs:
            rgb = kwargs[ATTR_RGB_COLOR]
            color = "#%02x%02x%02x" % tuple(rgb)
            LOGGER.debug("Setting ambient color to %s", color)
            await self.glimmr.set_ambient_color(color)
            return

        if ATTR_EFFECT in kwargs:
            effect = kwargs[ATTR_EFFECT]

            # Device-mode pseudo effects (Video, Audio, Hue Sync, ...).
            if effect in MODE_EFFECTS:
                mode = MODE_EFFECTS[effect]
                LOGGER.debug("Setting mode to %s", mode)
                await self.glimmr.set_mode(mode)
                return

            s_id = self.glimmr.get_scene_id_from_name(effect)
            if s_id in LEGACY_SCENE_MODES:
                mode = LEGACY_SCENE_MODES[s_id]
                LOGGER.debug("Setting mode to %s", mode)
                await self.glimmr.set_mode(mode)
                return

            LOGGER.debug("Setting ambient scene: %s", s_id)
            await self.glimmr.set_ambient_scene(s_id)
            return

        LOGGER.debug("Setting mode to %s", self.glimmr.system_data.previous_mode)
        await self.glimmr.set_mode(self.glimmr.system_data.previous_mode)

    async def async_turn_off(self, **kwargs: Any) -> None:
        """Instruct the light to turn off."""
        await self.glimmr.set_mode(0)

    # ------------------------------------------------------------------ #
    # State updates
    # ------------------------------------------------------------------ #

    async def async_update(self, force: bool = False) -> None:
        """Fetch new state data for this light."""
        await self.update_state(True)

    def update_state_available(self) -> None:
        """Update the state if the device is available."""
        self._state = self.glimmr.system_data.device_mode
        if self.glimmr.system_data.auto_disabled:
            self._state = 0
        self._available = True

    def update_state_unavailable(self) -> None:
        """Update the state if the device is unavailable."""
        self._state = 0
        self._available = False

    async def update_state(self, pull: bool) -> None:
        """Update the state."""
        try:
            if pull:
                await self.glimmr.update()
            self.update_state_available()
            self.update_color()
            self.update_effect()
            self.update_scene_list_sync()
        except TimeoutError as ex:
            LOGGER.debug(ex)
            self.update_state_unavailable()
        except GlimmrError as ex:
            LOGGER.debug(ex)
            self.update_state_unavailable()
        LOGGER.debug(
            "[glimmrlight %s] updated state, avail, scene: %s, %s, %s",
            self._attr_name,
            self._state,
            self._available,
            self._effect,
        )

    def update_color(self) -> None:
        """Update the rgb color from the device's ambient color hex string."""
        hex_color = self.glimmr.system_data.ambient_color or "FFFFFF"
        hex_color = hex_color.lstrip("#")
        try:
            self._rgb_color = (
                int(hex_color[0:2], 16),
                int(hex_color[2:4], 16),
                int(hex_color[4:6], 16),
            )
        except ValueError:
            LOGGER.debug("Invalid ambient color: %s", hex_color)

    def update_effect(self) -> None:
        """Derive the current effect from the device mode."""
        mode = self.glimmr.system_data.device_mode
        if mode in (0, 3):
            # Off / ambient: show the ambient scene name.
            scene = self.glimmr.system_data.ambient_scene
            self._effect = self.glimmr.get_scene_name_from_id(scene)
            return

        for name, m in MODE_EFFECTS.items():
            if m == mode:
                self._effect = name
                return

        self._effect = None

    def update_scene_list_sync(self) -> None:
        """Update the ambient scene list from the library cache."""
        self._scenes = list(self.glimmr.ambient_scenes.keys())

    # ------------------------------------------------------------------ #
    # Push (SignalR) plumbing
    # ------------------------------------------------------------------ #

    async def async_initialize_device(self) -> None:
        LOGGER.debug("Starting socket.")
        await self.hass.async_add_executor_job(self.glimmr.socket.start)
        self.glimmr.socket.on("olo", self._on_store)
        self.glimmr.socket.on("mode", self._on_mode)
        self.glimmr.socket.on_open(self._on_open)
        self.glimmr.socket.on_close(self._on_close)

    def _on_store(self, data) -> None:
        """Full store push from the device (runs on the SignalR thread)."""
        LOGGER.debug("Store update from ws.")
        asyncio.run_coroutine_threadsafe(self.update_state(False), self.hass.loop)
        self.schedule_update_ha_state()

    def _on_mode(self, mode) -> None:
        """Mode change push from the device (runs on the SignalR thread)."""
        LOGGER.debug("Mode update from ws: %s", mode[0])
        self.glimmr.system_data.device_mode = mode[0]
        self.update_state_available()
        self.update_effect()
        self.schedule_update_ha_state()

    def _on_open(self) -> None:
        LOGGER.debug("Socket connected.")

    def _on_close(self) -> None:
        LOGGER.debug("Socket closed.")
