# :bulb: glimmr_ha - V 0.1.1 (out for testing)

Initial commit.

## Installation via HACS:
WIP

## Install for testing

1. Logon to your HA or HASS with SSH
2. Got to the HA `custom_components` directory within the HA installation path (if this is not available - create this directory).
3. Run `cd custom_components`
4. Run `git clone https://github.com/d8ahazard/glimmr_ha` within the `custom_components` directory
5. Run `mv glimmr_ha/custom_components/glimmr_ha/* glimmr_ha/` to move the files in the correct diretory
6. Restart your HA/HASS service in the UI with `<your-URL>/config/server_control`
7. Add the bulbs either by:
   - HA UI by navigating to "Integrations" -> "Add Integration" -> "Glimmr"
   - Manually by adding them to `configuration.yaml`

Questions? Check out the github project [glimmr-python](https://github.com/d8ahazard/glimmr-python)

## Enable Debug
```YAML
logger:
    default: warning
    logs:
      homeassistant.components.glimmr_ha: debug
```

## HA config

## You can now use the HASS UI to add the devices/integration.

To enable the platform integration after installation add

```
light:
  - platform: glimmr_ha
    name: <Name of the device>
    host: <IP of the bulb>
  - platform: glimmr_ha
    name: <Name of the device#2>
    host: <IP of the bulb#2>
```

