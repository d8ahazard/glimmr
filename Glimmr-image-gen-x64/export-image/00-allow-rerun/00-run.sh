#!/bin/bash -e

if [ ! -x "${ROOTFS_DIR}/usr/bin/qemu-arm-static" ]; then
	cp /usr/bin/qemu-arm-static "${ROOTFS_DIR}/usr/bin/"
fi

# Note: the historical /etc/ld.so.preload disable/restore dance was only
# needed for the 32-bit raspi-copies-and-fills shim, which is no longer
# installed on this arm64 Bookworm image (see
# stage2/00-copies-and-fills/01-packages).
