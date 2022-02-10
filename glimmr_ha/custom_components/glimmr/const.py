"""Constants for Glimmr integration."""
from datetime import timedelta
import logging

# Integration domain
DOMAIN = "glimmr"
DEFAULT_NAME = "Glimmr"
LOGGER = logging.getLogger(__package__)
SCAN_INTERVAL = timedelta(seconds=10)

# Services
SERVICE_EFFECT = "effect"
