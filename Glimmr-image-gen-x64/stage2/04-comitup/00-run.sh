#!/bin/bash -e

set -e

# Allow ssh on first boot. On Bookworm this file is honoured both at
# /boot/ssh and /boot/firmware/ssh; the firstboot wizard reads the
# firmware partition.
touch "${ROOTFS_DIR}/boot/firmware/ssh"

# Pre-seed the first-boot user so userconf-pi creates the Glimmr login
# (glimmrtv/glimmrtv) non-interactively instead of running the account-setup
# wizard. This MUST match FIRST_USER_NAME/FIRST_USER_PASS in ./config and the
# defaults in script/publish.ps1; the hash is SHA-512 crypt of "glimmrtv".
install -m 644 files/userconf.txt "${ROOTFS_DIR}/boot/firmware/userconf.txt"

rm -f "${ROOTFS_DIR}/etc/network/interfaces"
install -m 644 files/interfaces "${ROOTFS_DIR}/etc/network/"

# Pull the davesteele apt-source meta-package. Comitup is still actively
# maintained for Bookworm/NetworkManager but is not in Debian; we install
# the maintainer's apt source which then pulls comitup itself in
# 01-packages.
# Set COMITUP_APT_SOURCE_DEB to pin an exact apt-source package filename
# (e.g. "davesteele-comitup-apt-source_1.3_all.deb") for reproducible builds;
# otherwise the newest version from pkgs.json is used (version-sorted, not
# file-order, so a re-ordered upstream listing can't hand us an old build).
if [ -n "${COMITUP_APT_SOURCE_DEB:-}" ]; then
	APT_DEB="${COMITUP_APT_SOURCE_DEB}"
else
	APT_DEB=$(curl -fsSL https://davesteele.github.io/comitup/pkgs.json \
		| grep apt-source_ \
		| tr -d ' ,"' \
		| sort -V \
		| tail -1)
fi

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
