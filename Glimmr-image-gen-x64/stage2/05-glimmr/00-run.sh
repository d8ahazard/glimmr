#!/bin/bash -e
install -m 644 files/comitup.conf "${ROOTFS_DIR}/etc/comitup.conf"
install -m 644 files/asound.state "${ROOTFS_DIR}/var/lib/alsa/asound.state"
install -m 644 -o 1000 -g 1000 files/.asoundrc "${ROOTFS_DIR}/home/${FIRST_USER_NAME}/.asoundrc"