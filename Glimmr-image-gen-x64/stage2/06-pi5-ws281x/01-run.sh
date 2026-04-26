#!/bin/bash -e
#
# Build the rp1_ws281x_pwm kernel module + device-tree overlay from the
# pi5 branch of jgarff/rpi_ws281x and stage them into the image. They
# are loaded at boot by /usr/local/sbin/glimmr-pi5-leds.sh (installed in
# stage2/05-glimmr) only when the running hardware is a Pi 5.
#
# This runs inside the qemu-user-static arm64 chroot so the module is
# built against the same headers/toolchain that ship in the image.

PI5_REPO_REF="${PI5_WS281X_REF:-pi5}"
PI5_REPO_URL="${PI5_WS281X_URL:-https://github.com/jgarff/rpi_ws281x.git}"

on_chroot << CHROOT_EOF
set -e

PI5_REPO_REF="${PI5_REPO_REF}"
PI5_REPO_URL="${PI5_REPO_URL}"

KVER=""
for d in /lib/modules/*-rpi-2712; do
    [ -d "\$d" ] || continue
    KVER="\$(basename "\$d")"
    break
done

if [ -z "\${KVER}" ]; then
    echo "WARN: no rpi-2712 kernel found in /lib/modules; skipping Pi 5 ws281x build"
    exit 0
fi

if [ ! -d "/lib/modules/\${KVER}/build" ]; then
    echo "WARN: kernel headers missing for \${KVER}; skipping Pi 5 ws281x build"
    exit 0
fi

WORK="\$(mktemp -d)"
trap 'rm -rf "\${WORK}"' EXIT

git clone --depth 1 --branch "\${PI5_REPO_REF}" \
    "\${PI5_REPO_URL}" "\${WORK}/rpi_ws281x"

cd "\${WORK}/rpi_ws281x/rp1_ws281x_pwm"

# The kernel-module Makefile defaults to /lib/modules/\$(uname -r)/build,
# which inside the qemu-user-static chroot resolves to the BUILD HOST's
# kernel rather than the Pi 5 target kernel. Pin it explicitly.
make KDIR="/lib/modules/\${KVER}/build" KERNELDIR="/lib/modules/\${KVER}/build"

install -D -m 644 rp1_ws281x_pwm.ko \
    "/lib/modules/\${KVER}/extra/rp1_ws281x_pwm.ko"
depmod -a "\${KVER}"

mkdir -p /usr/share/glimmr-pi5-leds
if [ -x ./dts.sh ]; then
    ./dts.sh
elif [ -f rp1_ws281x_pwm.dts ]; then
    dtc -@ -I dts -O dtb -o rp1_ws281x_pwm.dtbo rp1_ws281x_pwm.dts
fi

if [ -f rp1_ws281x_pwm.dtbo ]; then
    install -m 644 rp1_ws281x_pwm.dtbo \
        /usr/share/glimmr-pi5-leds/rp1_ws281x_pwm.dtbo
fi

# Note: deliberately not adding /etc/modules-load.d/. The Pi 3/4 kernel
# (rpi-v8) doesn't ship this module, so a global load entry would log a
# warning at every boot on those boards. glimmr-pi5-leds.service does
# the modprobe explicitly after detecting a Pi 5.
CHROOT_EOF
