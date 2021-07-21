#!/bin/bash -e
install -m 644 files/comitup.conf ${ROOTFS_DIR}/etc/comitup.conf
mkdir -p ${ROOTFS_DIR}/home/glimmrtv
cd ${ROOTFS_DIR}/home/glimmrtv
# Check dotnet installation
rm -rf ${ROOTFS_DIR}/home/glimmrtv/dotnet-sdk*
wget https://dotnetcli.blob.core.windows.net/dotnet/Sdk/master/dotnet-sdk-latest-linux-arm.tar.gz
sudo mkdir -p ${ROOTFS_DIR}/usr/share/dotnet
sudo mkdir -p /usr/share/dotnet
sudo tar -zxf dotnet-sdk-latest-linux-arm.tar.gz -C ${ROOTFS_DIR}/usr/share/dotnet
sudo ln -sf /usr/share/dotnet/dotnet /bin/dotnet
sudo ln -sf ${ROOTFS_DIR}/usr/share/dotnet/dotnet ${ROOTFS_DIR}/bin/dotnet
rm -rf ${ROOTFS_DIR}/home/glimmrtv/dotnet-sdk*

