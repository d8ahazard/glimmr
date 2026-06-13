#!/bin/bash -e

# Pi 3/4 (BCM2835/2711) PWM alt-function for GPIO 19 is ALT5.
# Pi 5 (BCM2712 via RP1) uses ALT3 for the PWM function on GPIO 18/19,
# and the rp1_ws281x_pwm kernel module + dtoverlay are loaded at boot
# by glimmr-pi5-leds.service (see files/glimmr-pi5-leds.service).
# Idempotent: re-runs (e.g. CONTINUE=1 builds) must not duplicate the block.
if ! grep -q "Glimmr LED output" "${ROOTFS_DIR}/boot/firmware/config.txt"; then
	cat >> "${ROOTFS_DIR}/boot/firmware/config.txt" <<'EOF'

# --- Glimmr LED output (WS281x via PWM) ---
[pi3]
gpio=19=op,a5
[pi4]
gpio=19=op,a5
[pi5]
gpio=19=op,a3
[all]
EOF
fi

echo "done" > "${ROOTFS_DIR}/home/${FIRST_USER_NAME}/firstrun"
mkdir -p "${ROOTFS_DIR}/usr/share/Glimmr"
tar zxf ./files/archive.tgz -C "${ROOTFS_DIR}/usr/share/Glimmr/"
chown -R 0:0 "${ROOTFS_DIR}/usr/share/Glimmr"
find "${ROOTFS_DIR}/usr/share/Glimmr" -type d -exec chmod 755 {} \;
find "${ROOTFS_DIR}/usr/share/Glimmr" -type f -exec chmod 644 {} \;
if [ -f "${ROOTFS_DIR}/usr/share/Glimmr/Glimmr" ]; then
   chmod 755 "${ROOTFS_DIR}/usr/share/Glimmr/Glimmr"
fi

# Drop the systemd unit. Run as root (Glimmr needs raw GPIO/PWM and
# binds to privileged ports). No `sudo` wrapper -- Bookworm's sudo now
# requires a password by default and a non-interactive ExecStart=sudo
# would deadlock the service.
install -m 644 files/glimmr.service "${ROOTFS_DIR}/etc/systemd/system/glimmr.service"

# Pi 5 WS281x boot helper: loads rp1_ws281x_pwm kernel module + dtoverlay
# and pinmuxes GPIO 19 to PWM3 alt3. No-op on Pi 3/4.
install -m 755 files/glimmr-pi5-leds.sh "${ROOTFS_DIR}/usr/local/sbin/glimmr-pi5-leds.sh"
install -m 644 files/glimmr-pi5-leds.service "${ROOTFS_DIR}/etc/systemd/system/glimmr-pi5-leds.service"

on_chroot << EOF
systemctl enable glimmr.service
systemctl enable glimmr-pi5-leds.service
EOF
