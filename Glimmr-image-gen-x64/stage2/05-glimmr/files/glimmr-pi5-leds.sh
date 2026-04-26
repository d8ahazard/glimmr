#!/bin/bash
# glimmr-pi5-leds.sh
#
# Boot-time helper that prepares the WS281x PWM path on Raspberry Pi 5.
# On Pi 3 and Pi 4 this script exits cleanly without touching anything --
# the legacy rpi_ws281x library handles those models directly.
#
# On Pi 5 the RP1 southbridge owns GPIO/PWM, so userspace cannot drive
# WS281x via the BCM2835 PWM block any more. Instead we:
#   1. Load the rp1_ws281x_pwm kernel module (built from the pi5 branch
#      of jgarff/rpi_ws281x during image generation).
#   2. Apply the rp1_ws281x_pwm device-tree overlay.
#   3. Set GPIO 19 to alt-function a3 (PWM channel 3) using pinctrl.
#
# Default channel is GPIO 19 / PWM channel 3, matching the Pi 3/4 wiring
# in this image. Override with /etc/default/glimmr-pi5-leds.

set -e

DEFAULTS=/etc/default/glimmr-pi5-leds
GLIMMR_PI5_GPIO=19
GLIMMR_PI5_PWM_CHANNEL=3
GLIMMR_PI5_ALT=a3

if [ -r "${DEFAULTS}" ]; then
    # shellcheck disable=SC1090
    . "${DEFAULTS}"
fi

MODEL_FILE=/proc/device-tree/model
if [ ! -r "${MODEL_FILE}" ]; then
    echo "glimmr-pi5-leds: no device-tree model file, skipping"
    exit 0
fi

MODEL=$(tr -d '\0' < "${MODEL_FILE}")
case "${MODEL}" in
    *"Raspberry Pi 5"*|*"Raspberry Pi Compute Module 5"*)
        ;;
    *)
        echo "glimmr-pi5-leds: not a Pi 5 (${MODEL}), skipping"
        exit 0
        ;;
esac

if ! lsmod | grep -q '^rp1_ws281x_pwm'; then
    echo "glimmr-pi5-leds: loading rp1_ws281x_pwm (channel=${GLIMMR_PI5_PWM_CHANNEL})"
    modprobe rp1_ws281x_pwm "pwm_channel=${GLIMMR_PI5_PWM_CHANNEL}"
fi

OVERLAY_DIR=/usr/share/glimmr-pi5-leds
if [ -f "${OVERLAY_DIR}/rp1_ws281x_pwm.dtbo" ] && command -v dtoverlay >/dev/null 2>&1; then
    if ! dtoverlay -l 2>/dev/null | grep -q rp1_ws281x_pwm; then
        echo "glimmr-pi5-leds: applying rp1_ws281x_pwm overlay"
        dtoverlay -d "${OVERLAY_DIR}" rp1_ws281x_pwm || true
    fi
fi

if command -v pinctrl >/dev/null 2>&1; then
    echo "glimmr-pi5-leds: pinctrl set ${GLIMMR_PI5_GPIO} ${GLIMMR_PI5_ALT} pn"
    pinctrl set "${GLIMMR_PI5_GPIO}" "${GLIMMR_PI5_ALT}" pn || true
fi

exit 0
