#!/bin/bash
branch=${1:-"master"}
APPDIR=${2:-"/home/glimmrtv/glimmr"}

PUBPROFILE="Linux"
PUBPATH="linux-arm"

if [ -f "/usr/bin/raspi-config" ] 
  then
    PUBPROFILE="LinuxARM"
    PUBPATH="linux-arm"
fi

unameOut="$(uname -s)"
if [ $unameOut == "Darwin" ]
  then
    PUBPROFILE="OSX"
    PUBPATH="osx"
fi

if [ $unameOut == "FreeBSD" ]
  then
    PUBPROFILE="Portable"
    PUBPATH="portable"
fi

echo "Updating Glimmr for $PUBPROFILE using branch $branch."

log=$(ls -t /var/log/glimmr/glimmr* | head -1)
if [ $log == "" ]
  then
    $log = /var/log/glimmr/glimmr.log
fi

if [ ! -f $log ]
  then
    log = /var/log/glimmr/glimmr.log
    touch $log
    chmod 777 $log
fi

#Stop service
echo "Stopping glimmr..." >> $log
service glimmr stop
echo "SERVICE STOPPED!" >> $log

if [ ! -d "/opt/glimmr" ]
  then
# Make dir
  mkdir /opt/glimmr  
fi

# Download and extract latest release
cd /tmp || exit
ver=$(wget "https://api.github.com/repos/d8ahazard/glimmr/releases/latest" -q -O - | grep '"tag_name":' | sed -E 's/.*"([^"]+)".*/\1/')
echo ver is $ver
url="https://github.com/d8ahazard/glimmr/releases/download/$ver/Glimmr-$PUBPATH-$ver.tgz"
echo Grabbing archive from $url
wget -O archive.tgz $url
tar zxvf ./archive.tgz -C /opt/glimmr/
chmod -R 777 /opt/glimmr/
rm ./archive.tgz
echo "DONE." >> $log

echo "Restarting glimmr service..." >> $log

# Restart Service
service glimmr start