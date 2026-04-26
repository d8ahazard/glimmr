#!/bin/bash -e

set -e

# Allow ssh on first boot. On Bookworm this file is honoured both at
# /boot/ssh and /boot/firmware/ssh; the firstboot wizard reads the
# firmware partition.
touch "${ROOTFS_DIR}/boot/firmware/ssh"

install -m 644 files/userconf.txt "${ROOTFS_DIR}/boot/firmware/userconf.txt"

rm -f "${ROOTFS_DIR}/etc/network/interfaces"
install -m 644 files/interfaces "${ROOTFS_DIR}/etc/network/"

# Pull the davesteele apt-source meta-package. Comitup is still actively
# maintained for Bookworm/NetworkManager but is not in Debian; we install
# the maintainer's apt source which then pulls comitup itself in
# 01-packages.
APT_DEB=$(curl -fsSL https://davesteele.github.io/comitup/pkgs.json \
	| grep apt-source_ \
	| tail -1 \
	| tr -d ' ,"')

if [ -z "${APT_DEB}" ]; then
	echo "ERROR: could not determine davesteele-comitup-apt-source filename" >&2
	exit 1
fi

curl -fsSL -o "${ROOTFS_DIR}/tmp/${APT_DEB}" \
	"https://davesteele.github.io/comitup/deb/${APT_DEB}"

on_chroot << EOF
dpkg -i --force-all "/tmp/${APT_DEB}"
apt-get -f install -y
apt-get update

# Comitup expects to own DNS + DHCP for its captive portal, so mask
# the system services that would otherwise fight it.
systemctl mask dnsmasq.service
systemctl mask systemd-resolved.service

# Note: we deliberately don't mask wpa_supplicant.service or
# dhcpcd.service here. On Bookworm wpa_supplicant is started on demand
# by NetworkManager (masking it breaks Wi-Fi entirely) and dhcpcd is
# not installed at all on a NetworkManager-only image.
EOF

rm -f "${ROOTFS_DIR}/tmp/${APT_DEB}"
