"""Glimmr integration."""
from __future__ import annotations

from typing import List, Any, Tuple, Set

import homeassistant.helpers.config_validation as cv
import voluptuous as vol
from glimmr import Glimmr
from glimmr.exceptions import GlimmrConnectionError, GlimmrError
# Import the device class from the component
from homeassistant.components.light import (
    ATTR_EFFECT,
    ATTR_RGB_COLOR,
    COLOR_MODE_RGB,
    PLATFORM_SCHEMA,
    SUPPORT_EFFECT,
    LightEntity,
)
from homeassistant.const import CONF_HOST, CONF_NAME, CONF_MAC
from homeassistant.util import slugify

from .const import DOMAIN, LOGGER

PLATFORM_SCHEMA = PLATFORM_SCHEMA.extend(
    {vol.Required(CONF_HOST): cv.string,
     vol.Required(CONF_NAME): cv.string,
     vol.Required(CONF_MAC): cv.string}
)


async def async_setup_platform(hass, config, async_add_entities, discovery_info=None):
    """Set up the Glimmr platform from legacy config."""
    # Assign configuration variables.
    # The configuration check takes care they are present.
    ip_address = config[CONF_HOST]
    try:
        bulb = Glimmr(ip_address)
        # Add devices
        LOGGER.debug("Creating light %s", ip_address)
        async_add_entities([GlimmrLight(bulb)], update_before_add=True)
        return True
    except GlimmrConnectionError:
        LOGGER.error("Can't add device with ip %s.", ip_address)
        return False


async def async_setup_entry(hass, entry, async_add_entities):
    """Set up the Glimmr platform from config_flow."""
    # Assign configuration variables.
    light = hass.data[DOMAIN][entry.unique_id]
    LOGGER.debug("Setting up glimmr: ")
    glimmr_light = GlimmrLight(light)
    LOGGER.debug("ASE", glimmr_light)
    # Add devices with defined name
    async_add_entities([glimmr_light], update_before_add=True)

    # Register services
    async def async_update(call=None):
        """Trigger update."""
        LOGGER.debug("[glimmr light %s] update requested", entry.data.get(CONF_HOST))
        await glimmr_light.async_update()
        await glimmr_light.async_update_ha_state()

    service_name = slugify(f"{entry.data.get(CONF_NAME)} updateService")
    hass.services.async_register(DOMAIN, service_name, async_update)
    return True


class GlimmrLight(LightEntity):
    _attr_icon = "mdi:led-strip-variant"
    """Representation of Glimmr device."""

    def __init__(self, glimmr: Glimmr):
        """Initialize an Glimmr."""
        LOGGER.debug("Initializing light...")
        self.glimmr: Glimmr = glimmr
        self.glimmr.LOGGER = LOGGER
        self.glimmr.system_data = glimmr.system_data
        self._state = self.glimmr.system_data.device_mode
        if self.glimmr.system_data.auto_disabled:
            self._state = 0
        self._brightness = 255
        self._name = self.glimmr.system_data.device_name
        self.rgb_color = bytes.fromhex(self.glimmr.system_data.ambient_color)
        self._available = None
        self._effect = self.glimmr.system_data.ambient_scene
        self._scenes: List[str] = []
        self.update_scene_list()

    async def async_added_to_hass(self):
        """Register device notification."""
        LOGGER.debug("Added, connecting to ws.")
        await self.async_initialize_device()

    async def async_will_remove_from_hass(self) -> None:
        if self.glimmr.connected:
            LOGGER.debug("Disconnecting from ws.")
            self.glimmr.socket.stop()

    def turn_off(self, **kwargs: Any) -> None:
        self.glimmr.set_mode(0)

    def turn_on(self, **kwargs: Any) -> None:
        """Instruct the light to turn on."""
        if ATTR_RGB_COLOR in kwargs:
            self.glimmr.set_ambient_color(kwargs.get(ATTR_RGB_COLOR))
            return

        if ATTR_EFFECT in kwargs:
            scene_id = self.glimmr.get_scene_id_from_name(kwargs[ATTR_EFFECT])

            if scene_id < -1:  # rhythm
                mode = 0
                if scene_id == -2:
                    mode = 1
                if scene_id == -3:
                    mode = 2
                if scene_id == -4:
                    mode = 3
                if scene_id == -5:
                    mode = 4
                if scene_id == -6:
                    mode = 5
                if scene_id == -7:
                    mode = 6
                LOGGER.debug("Setting mode to %s", mode)
                self.glimmr.set_mode(mode)
                return
            else:
                LOGGER.debug("Setting ambient scene: %s", scene_id)
                self.glimmr.system_data.ambient_scene = scene_id
                self.glimmr.set_ambient_scene(scene_id)
                return

        else:
            self.glimmr.set_mode(self.glimmr.system_data.previous_mode)

    @property
    def brightness(self):
        """Unused."""
        return self._brightness

    @property
    def supported_features(self) -> int:
        return SUPPORT_EFFECT

    @property
    def rgb_color(self) -> Tuple[int, int, int]:
        """Return the ambient color property."""
        LOGGER.debug("RGBCOL")
        LOGGER.debug("Color: %s", self._rgb_color)
        return self._rgb_color

    @property
    def name(self):
        """Return the ip as name of the device if any."""
        return self._name

    @property
    def unique_id(self):
        """Return light unique_id."""
        return self.glimmr.system_data.device_id

    @property
    def is_on(self):
        """Return true if light is on."""
        return self._state != 0

    async def async_turn_on(self, **kwargs):
        """Instruct the light to turn on."""

        if ATTR_RGB_COLOR in kwargs:
            rgb = kwargs[ATTR_RGB_COLOR]
            color = '#%02x%02x%02x' % rgb
            LOGGER.debug("Setting ambient color to " + color)
            await self.glimmr.set_ambient_color(color)
            return

        if ATTR_EFFECT in kwargs:
            scene_id = kwargs[ATTR_EFFECT]
            s_id = self.glimmr.get_scene_id_from_name(scene_id)
            if s_id < -1:
                mode = self.glimmr.system_data.previous_mode
                if s_id == -2:
                    mode = 1
                if s_id == -3:
                    mode = 2
                if s_id == -4:
                    mode = 3
                if s_id == -5:
                    mode = 4
                if s_id == -6:
                    mode = 5
                if s_id == -7:
                    mode = 6
                await self.glimmr.set_mode(mode)
                return
            else:
                LOGGER.debug(
                    "[glimmrlight %s] Setting ambient scene: %s",
                    self.glimmr.system_data.device_name,
                    s_id
                )
                await self.glimmr.set_ambient_scene(s_id)

        else:
            LOGGER.debug("Setting mode to %s", self.glimmr.system_data.previous_mode)
            await self.glimmr.set_mode(self.glimmr.system_data.previous_mode)

    async def async_turn_off(self, **kwargs):
        """Instruct the light to turn off."""
        await self.glimmr.set_mode(0)

    @property
    def should_poll(self) -> bool:
        """Update the state periodically."""
        return self.glimmr.connected is False

    @property
    def supported_color_modes(self) -> Set[str]:
        return {COLOR_MODE_RGB}

    @property
    def effect(self):
        """Return the current effect."""
        LOGGER.debug("Cur effect requested: %s", self._effect)
        return self._effect

    @property
    def effect_list(self):
        """Return the list of supported effects.

        URL: https://docs.pro.glimmrconnected.com/#light-modes
        """
        return self._scenes

    @property
    def available(self):
        """Return if light is available."""
        return self._available

    async def async_update(self, force=False):
        """Fetch new state data for this light."""
        LOGGER.debug("Forcing state update.")
        await self.update_state(force)

        if self._state is not None and self._state is not False and force is True:
            LOGGER.debug("Updating scene list.")
            await self.update_scene_list()

    @property
    def device_info(self):
        """Get device specific attributes."""
        LOGGER.debug(
            "[glimmrlight %s] Call device info...",
            self._name
        )
        return {
            "identifiers": {(DOMAIN, self._name)},
            "name": self._name,
            "manufacturer": "D8ahazard",
            "model": "Glimmr",
        }

    @property
    def color_mode(self) -> str:
        return COLOR_MODE_RGB

    def update_state_available(self):
        """Update the state if bulb is available."""
        self._state = self.glimmr.system_data.device_mode
        if self.glimmr.system_data.auto_disabled:
            self._state = 0
        self._available = True

    def update_state_unavailable(self):
        """Update the state if bulb is unavailable."""
        self._state = 0
        self._available = False

    async def update_state(self, pull: bool):
        """Update the state."""
        try:
            if pull:
                await self.glimmr.update()
            self.update_state_available()
            self.update_color()
            self.update_effect()
            self.update_mode()
            await self.update_scene_list()
        except TimeoutError as ex:
            LOGGER.debug(ex)
            self.update_state_unavailable()
        except GlimmrError as ex:
            LOGGER.debug(ex)
            self.update_state_unavailable()
        LOGGER.debug(
            "[glimmrlight %s] updated state, avail, scene: %s, %s, %s",
            self._name,
            self._state,
            self._available,
            self._effect
        )

    async def async_effect(
            self,
            effect: int | None = None
    ) -> None:
        """Set the effect of a Glimmr light."""
        await self.glimmr.set_ambient_scene(effect)

    def update_color(self):
        """Update the hs color."""
        hex_color = self.glimmr.system_data.ambient_color
        LOGGER.debug("Updating color from " + self.glimmr.system_data.ambient_color)
        r_hex = hex_color[0:2]
        g_hex = hex_color[2:4]
        b_hex = hex_color[4:6]
        self._rgb_color = int(r_hex, 16), int(g_hex, 16), int(b_hex, 16)
        LOGGER.debug("Updated color: %s", self._rgb_color)

    def update_effect(self):
        """Update the bulb scene."""
        mode = self.glimmr.system_data.device_mode
        effect = 0
        if mode == 0:
            effect = self.glimmr.system_data.ambient_scene
        if mode == 1:
            effect = -2
        if mode == 2:
            effect = -3
        if mode == 3:
            effect = self.glimmr.system_data.ambient_scene
        if mode == 4:
            effect = -5
        if mode == 5:
            effect = -6
        LOGGER.debug("Effect id set to: %s", effect)
        self._effect = self.glimmr.get_scene_name_from_id(effect)

    async def update_scene_list(self):
        """Update the scene list."""
        LOGGER.debug("Updating scene list...")
        _value = self.glimmr.ambient_scenes
        self._scenes = list(_value.keys())
        LOGGER.debug("Updating scene list: %s", self._scenes)

    def update_mode(self):
        pass

    @rgb_color.setter
    def rgb_color(self, value):
        self._rgb_color = value

    def update_data(self, data):
        LOGGER.debug("Updating from ws!")
        self.update_state(False)
        super().schedule_update_ha_state(True)

    async def async_initialize_device(self):
        LOGGER.debug("Starting socket.")
        await self.hass.async_add_executor_job(self.glimmr.socket.start)
        self.glimmr.socket.on("olo", self.update_data)
        self.glimmr.socket.on("mode", self.mode_changed)
        self.glimmr.socket.on("stats", self.stats)
        self.glimmr.socket.on("log", self.log)
        self.glimmr.socket.on("frames", self.frames)
        self.glimmr.socket.on_open(self.connected)
        self.glimmr.socket.on_close(self.closed)

    def mode_changed(self, mode):
        LOGGER.debug("Modechange...")
        LOGGER.debug("Updating mode from ws: %s", mode[0])
        self.glimmr.system_data.device_mode = mode[0]
        super().schedule_update_ha_state(True)

    def stats(self, stats):
        LOGGER.debug("Oooh, stats: ", stats)
        pass

    def log(self, msg):
        pass

    def frames(self, frames):
        pass

    def connected(self):
        pass

    def closed(self):
        pass
