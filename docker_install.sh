#!/bin/bash

export DEBIAN_FRONTEND=noninteractive

if [ ! -d "/opt/glimmr" ]
  then
# Make dir
  mkdir /opt/glimmr  
fi

# Download and extract latest release
cd /tmp || exit
ver=$(wget "https://api.github.com/repos/d8ahazard/glimmr/releases/latest" -q -O - | grep '"tag_name":' | sed -E 's/.*"([^"]+)".*/\1/')
url="https://github.com/d8ahazard/glimmr/releases/download/$ver/Glimmr-linux-$ver.tgz"
wget -O archive.tgz $url
tar zxvf ./archive.tgz -C /opt/glimmr/
chmod -R 777 /opt/glimmr/
rm ./archive.tgz
