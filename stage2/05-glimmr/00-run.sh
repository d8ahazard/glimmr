#!/bin/bash -e
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel Current --install-dir /opt/dotnet
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel Current --architecture arm --install-dir ${ROOTFS_DIR}/opt/dotnet
install -m 644 files/comitup.conf ${ROOTFS_DIR}/etc/comitup.conf
install -m 644 files/ws2811.so ${ROOTFS_DIR}/usr/lib/ws2811.so
mkdir -p ${ROOTFS_DIR}/home/glimmr