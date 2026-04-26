#!/bin/bash -e

# On Bookworm wireless is owned by NetworkManager. The legacy
# /etc/wpa_supplicant/wpa_supplicant.conf path is no longer consulted by
# the default stack, so we don't drop a stub there any more.
#
# We still let raspi-config set the wireless regulatory domain (this
# writes to /etc/default/crda + /var/lib/systemd/rfkill via the modern
# path) when WPA_COUNTRY is provided in the build environment, and we
# leave the rfkill state alone otherwise -- NetworkManager + the rfkill
# kernel default handle unblock once the country is known on first boot.

if [ -v WPA_COUNTRY ]; then
	on_chroot <<- EOF
		SUDO_USER="${FIRST_USER_NAME}" raspi-config nonint do_wifi_country "${WPA_COUNTRY}"
	EOF
fi
