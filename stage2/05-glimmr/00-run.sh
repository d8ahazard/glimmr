#!/bin/bash -e
install -m 644 files/comitup.conf ${ROOTFS_DIR}/etc/comitup.conf
install -m 644 files/ws2811.so ${ROOTFS_DIR}/usr/lib/ws2811.so