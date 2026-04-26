#!/bin/bash

set -e

for conn in dhcp static; do
    install -m 600 "files/${conn}.nmconnection" \
        "${ROOTFS_DIR}/etc/NetworkManager/system-connections/"
done

install -D -m 755 files/set-comitup-hostname-once.sh \
    "${ROOTFS_DIR}/usr/local/sbin/set-comitup-hostname-once.sh"
install -D -m 644 files/set-comitup-hostname-once.service \
    "${ROOTFS_DIR}/etc/systemd/system/set-comitup-hostname-once.service"

on_chroot <<EOF
systemctl enable set-comitup-hostname-once.service
systemctl enable NetworkManager.service
EOF
