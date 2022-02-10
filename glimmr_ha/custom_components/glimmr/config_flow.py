"""Config flow to configure the Glimmr integration."""
from __future__ import annotations

from typing import Any

import voluptuous as vol
from glimmr import Glimmr, GlimmrConnectionError
from homeassistant.config_entries import (
    SOURCE_ZEROCONF,
    ConfigEntry,
    ConfigFlow,
    OptionsFlow,
)
from homeassistant.const import CONF_HOST, CONF_MAC, CONF_NAME
from homeassistant.core import callback
from homeassistant.data_entry_flow import FlowResult
from homeassistant.helpers.typing import DiscoveryInfoType

from .const import DOMAIN, LOGGER


class GlimmrFlowHandler(ConfigFlow, domain=DOMAIN):
    """Handle a Glimmr config flow."""

    VERSION = 1

    @staticmethod
    @callback
    def async_get_options_flow(config_entry: ConfigEntry) -> GlimmrOptionsFlowHandler:
        """Get the options flow for this handler."""
        return GlimmrOptionsFlowHandler(config_entry)

    async def async_step_user(
        self, user_input: dict[str, Any] | None = None
    ) -> FlowResult:
        """Handle a flow initiated by the user."""
        return await self._handle_config_flow(user_input)

    async def async_step_zeroconf(
        self, discovery_info: DiscoveryInfoType
    ) -> FlowResult:
        """Handle zeroconf discovery."""

        # Hostname is format: glimmr-xxx.local.
        host = discovery_info["hostname"].rstrip(".")
        name = host.rsplit(".")[0]

        self.context.update(
            {
                CONF_HOST: discovery_info["host"],
                CONF_NAME: name,
                CONF_MAC: discovery_info["properties"].get(CONF_MAC),
                "title_placeholders": {"name": name},
            }
        )

        # Prepare configuration flow
        return await self._handle_config_flow(discovery_info, True)

    async def async_step_zeroconf_confirm(
        self, user_input: dict[str, Any] | None = None
    ) -> FlowResult:
        """Handle a flow initiated by zeroconf."""
        return await self._handle_config_flow(user_input)

    async def _handle_config_flow(
        self, user_input: dict[str, Any] | None = None, prepare: bool = False
    ) -> FlowResult:
        """Config flow handler for Glimmr."""
        source = self.context.get("source")

        # Request user input, unless we are preparing discovery flow
        if user_input is None and not prepare:
            if source == SOURCE_ZEROCONF:
                return self._show_confirm_dialog()
            return self._show_setup_form()

        # if prepare is True, user_input can not be None.
        assert user_input is not None

        if source == SOURCE_ZEROCONF:
            user_input[CONF_HOST] = self.context.get(CONF_HOST)
            user_input[CONF_MAC] = self.context.get(CONF_MAC)

        if user_input.get(CONF_MAC) is None or not prepare:
            LOGGER.debug("Creating glimmr from config flow (NO mac/not prepare) " + user_input[CONF_HOST])
            glimmr = Glimmr(user_input[CONF_HOST])
            await glimmr.update()
            glimmr.LOGGER = LOGGER
            try:
                await glimmr.update()
            except GlimmrConnectionError:
                if source == SOURCE_ZEROCONF:
                    return self.async_abort(reason="cannot_connect")
                return self._show_setup_form({"base": "cannot_connect"})
            user_input[CONF_MAC] = glimmr.system_data.device_id

        # Check if already configured
        await self.async_set_unique_id(user_input[CONF_MAC])
        self._abort_if_unique_id_configured(updates={CONF_MAC: user_input[CONF_MAC]})

        title = user_input[CONF_HOST]
        if source == SOURCE_ZEROCONF:
            title = self.context.get(CONF_NAME)

        if prepare:
            return await self.async_step_zeroconf_confirm()

        return self.async_create_entry(
            title=title,
            data={CONF_HOST: user_input[CONF_HOST], CONF_MAC: user_input[CONF_MAC]},
        )

    def _show_setup_form(self, errors: dict | None = None) -> FlowResult:
        """Show the setup form to the user."""
        return self.async_show_form(
            step_id="user",
            data_schema=vol.Schema({vol.Required(CONF_HOST): str}),
            errors=errors or {},
        )

    def _show_confirm_dialog(self, errors: dict | None = None) -> FlowResult:
        """Show the confirm dialog to the user."""
        name = self.context.get(CONF_NAME)
        return self.async_show_form(
            step_id="zeroconf_confirm",
            description_placeholders={"name": name},
            errors=errors or {},
        )


class GlimmrOptionsFlowHandler(OptionsFlow):
    """Handle Glimmr options."""

    def __init__(self, config_entry: ConfigEntry) -> None:
        """Initialize Glimmr options flow."""
        self.config_entry = config_entry

    async def async_step_init(
        self, user_input: dict[str, Any] | None = None
    ) -> FlowResult:
        """Manage Glimmr options."""
        if user_input is not None:
            return self.async_create_entry(title="", data=user_input)

        return self.async_show_form(
            step_id="init"
        )
