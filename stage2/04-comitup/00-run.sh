#!/bin/bash -e

set -e

touch ${ROOTFS_DIR}/boot/ssh

mv ${ROOTFS_DIR}/etc/wpa_supplicant/wpa_supplicant.conf ${ROOTFS_DIR}/etc/wpa_supplicant/wpa_supplicant.conf.comitup_disable

rm -f ${ROOTFS_DIR}/etc/network/interfaces
install -m 644 files/interfaces ${ROOTFS_DIR}/etc/network/

APT_DEB=$(curl https://davesteele.github.io/comitup/pkgs.json 2>&1 | grep apt-source_ | tail -1 | tr -d " ,\"")
wget -P ${ROOTFS_DIR}/tmp https://davesteele.github.io/comitup/deb/${APT_DEB}

on_chroot << EOF
dpkg -i --force-all /tmp/davesteele-comitup-apt-source_*.deb
apt-get -f install
apt-get update
systemctl mask dnsmasq.service
systemctl mask systemd-resolved.service
systemctl mask dhcpd.service
systemctl mask dhcpcd.service
EOF

rm ${ROOTFS_DIR}/tmp/${APT_DEB}
