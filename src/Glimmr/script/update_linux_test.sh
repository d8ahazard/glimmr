#!/bin/bash

arch="$(arch)"
if [ "$arch" == "armv71" ] 
  then
    PUBPROFILE="LinuxARM"
    PUBPATH="linux-arm"
  else
    echo "Invalid arch $arch?"
    exit 1
fi

# shellcheck disable=SC2012
log=$(ls -t /var/log/glimmr/glimmr* | head -1)
if [ "$log" == "" ]
  then
    log=/var/log/glimmr/glimmr.log
fi

if [ ! -f $log ]
  then
    log=/var/log/glimmr/glimmr.log
    touch $log
    chmod 777 $log
fi

echo "Checking for Glimmr updates for $PUBPROFILE." >> $log

if [ ! -d "/usr/share/Glimmr" ]
  then
# Make dir
  mkdir /usr/share/Glimmr  
fi

echo "Stopping glimmr services..." >> $log
service glimmr stop
echo "Services stopped." >> $log

cd /tmp || exit
url="https://drive.google.com/u/0/uc?id=182aa-0mmgUeEL7RbG8QW5UV_cF_0_QMG&export=download"
echo "Grabbing archive from $url" >> $log
wget -O archive.tgz "$url"
tar zxvf ./archive.tgz -C /usr/share/Glimmr/
chmod -R 777 /usr/share/Glimmr/
rm ./archive.tgz
echo "Update completed." >> $log
echo "$ver" > /etc/Glimmr/version
echo "Restarting glimmr service..." >> $log

# Restart Service
service glimmr start