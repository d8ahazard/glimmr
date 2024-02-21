#!/bin/bash -e
echo "gpio=19=op,a5" >> ${ROOTFS_DIR}/boot/config.txt
echo "done" > "${ROOTFS_DIR}/home/glimmrtv/firstrun"
mkdir -p ${ROOTFS_DIR}/usr/share/Glimmr  
# Download and extract latest release
tar zxvf ./files/archive.tgz -C ${ROOTFS_DIR}/usr/share/Glimmr/
chmod -R 777 ${ROOTFS_DIR}/usr/share/Glimmr/

# Install service
echo "
[Unit]
Description=GlimmrTV

[Service]
Type=simple
RemainAfterExit=yes
StandardOutput=tty
Restart=always
User=root
WorkingDirectory=/usr/share/Glimmr
ExecStart=sudo ./Glimmr
KillMode=process

[Install]
WantedBy=multi-user.target

" > ${ROOTFS_DIR}/etc/systemd/system/glimmr.service

if [ -f "${ROOTFS_DIR}/etc/ld.so.preload" ]; then
   mv "${ROOTFS_DIR}/etc/ld.so.preload" "${ROOTFS_DIR}/etc/ld.so.preload.disabled"
fi
