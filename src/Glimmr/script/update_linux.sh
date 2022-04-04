#!/bin/bash
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

arch="$(arch)"
PUBPROFILE="Linux"
PUBPATH="linux-x64"

if [ -f "/usr/bin/raspi-config" ] && [ "$arch" == "armv71" ] 
  then
    PUBPROFILE="LinuxARM"
    PUBPATH="linux-arm"
fi

if [ -f "/usr/bin/raspi-config" ] && [ "$arch" == "aarch64" ] 
  then
    PUBPROFILE="LinuxARM64"
    PUBPATH="linux-arm64"
fi

echo "Checking for Glimmr updates for $PUBPROFILE." >> $log
echo "Checking for Glimmr updates for $PUBPROFILE."

if [ ! -d "/usr/share/Glimmr" ]
  then
# Make dir
  mkdir /usr/share/Glimmr  
fi

# Download and extract latest release
ver=$(wget "https://api.github.com/repos/d8ahazard/glimmr/releases/latest" -q -O - | grep '"tag_name":' | sed -E 's/.*"([^"]+)".*/\1/')
echo "Repo version is $ver." >> $log
echo "Repo version is $ver."
cd /tmp || exit

echo "Updating glimmr to version $ver." >> $log
echo "Updating glimmr to version $ver."
url="https://github.com/d8ahazard/glimmr/releases/download/$ver/Glimmr.$ver.$PUBPATH.tar.gz"
echo "Grabbing archive from $url" >> $log
echo "Grabbing archive from $url"
wget -O archive.tgz "$url"

# Stop service
echo "Stopping glimmr services..." >> $log
echo "Stopping glimmr services..."
service glimmr stop
echo "Services stopped." >> $log
echo "Services stopped."

# Extract
echo "Extracting archive..." >> $log
echo "Extracting archive..."
tar zxvf ./archive.tgz -C /usr/share/Glimmr/

# Permissions and cleanup
echo "Setting permissions..." >> $log
echo "Setting permissions..."
chmod -R 777 /usr/share/Glimmr/
echo "Cleanup..." >> $log
echo "Cleanup..."
rm ./archive.tgz
echo "Update completed." >> $log
echo "Update completed."
echo "$ver" > /etc/Glimmr/version
echo "Restarting glimmr service..." >> $log
echo "Restarting glimmr service..."

# Restart Service
service glimmr start