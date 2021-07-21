#!/bin/bash -e
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel Current --install-dir /opt/dotnet
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel Current --install-dir ${ROOTFS_DIR}/opt/dotnet
install -m 644 files/comitup.conf ${ROOTFS_DIR}/etc/comitup.conf
mkdir -p ${ROOTFS_DIR}/home/glimmr