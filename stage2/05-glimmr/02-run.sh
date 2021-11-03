#!/bin/bash -e
echo "gpio=19=op,a5" >> ${ROOTFS_DIR}/boot/config.txt
echo "done" > "${ROOTFS_DIR}/home/glimmrtv/firstrun"
if [! -d "${ROOTF_DIR}/opt/glimmr/" ]
	then
		mkdir ${ROOTFS_DIR}/opt/glimmr  
fi

# Download and extract latest release
cd /tmp || exit
ver=$(wget "https://api.github.com/repos/d8ahazard/glimmr/releases/latest" -q -O - | grep '"tag_name":' | sed -E 's/.*"([^"]+)".*/\1/')
url="https://github.com/d8ahazard/glimmr/releases/download/$ver/Glimmr-linux-arm-$ver.tgz"
wget -O /tmp/archive.tgz $url
tar zxvf /tmp/archive.tgz -C ${ROOTFS_DIR}/opt/glimmr/
chmod -R 777 ${ROOTFS_DIR}/opt/glimmr/
rm /tmp/archive.tgz

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
WorkingDirectory=/opt/glimmr
ExecStart=sudo Glimmr
KillMode=process

[Install]
WantedBy=multi-user.target

" > ${ROOTFS_DIR}/etc/systemd/system/glimmr.service