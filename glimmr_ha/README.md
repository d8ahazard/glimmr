# :bulb: glimmr_ha

Home Assistant integration for [Glimmr](https://github.com/d8ahazard/glimmr), exposing your
Glimmr device as a light entity with mode switching (Video, Audio, Ambient, A/V, Streaming,
DreamScreen, Hue Sync) via effects, ambient color control, and live state updates over SignalR.

## Installation via HACS

Add this repository (`https://github.com/d8ahazard/glimmr_ha`) as a custom repository in HACS,
then install "Glimmr" and restart Home Assistant.

## Manual install

1. Log on to your HA host with SSH.
2. Go to the `custom_components` directory within your HA configuration path
   (create it if it doesn't exist).
3. Copy `custom_components/glimmr` from this repository into your `custom_components`
   directory, so you end up with `custom_components/glimmr/manifest.json` etc.
4. Restart Home Assistant.
5. Glimmr devices on your network are discovered automatically (zeroconf). You can also add
   one manually from "Settings" -> "Devices & Services" -> "Add Integration" -> "Glimmr".

Questions? Check out the GitHub project [glimmr-python](https://github.com/d8ahazard/glimmr-python).

## Usage

- **On/Off**: turning the light off sets Glimmr to mode 0; turning it on restores the last mode.
- **Color**: setting an RGB color updates the ambient color.
- **Effects**: the effect list contains your ambient scenes plus `Mode: ...` entries for
  switching device modes (Video, Audio, Ambient, Audio/Video, Streaming, DreamScreen, Hue Sync).
- **Service**: `glimmr.update` forces a state refresh.

## Enable debug logging

```yaml
logger:
  default: warning
  logs:
    custom_components.glimmr: debug
```
