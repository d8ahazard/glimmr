#!/bin/bash -e
dpkg-reconfigure locales # add the en_GB.UTF-8 locale and set as default
export LANGUAGE=en_US.UTF-8 
export LANG=en_US.UTF-8
export LC_ALL=en_US.UTF-8
locale-gen en_US.UTF-8
update-locale en_US.UTF-8

if [ ! -d "${ROOTFS_DIR}" ] || [ "${USE_QCOW2}" = "1" ]; then
	bootstrap ${RELEASE} "${ROOTFS_DIR}" http://raspbian.raspberrypi.org/raspbian/
fi
