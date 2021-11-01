#!/bin/bash

export DEBIAN_FRONTEND=noninteractive

# Add extra repos for users on Ubuntu 20- for libjasper-dev
add-apt-repository "deb http://security.ubuntu.com/ubuntu xenial-security main"

# Add extra repos for users on Ubuntu 20- for libqtgui4 libqt4-test
add-apt-repository ppa:rock-core/qt4 -y
apt-get -y update && apt-get -y upgrade

apt-get -y install libgtk-3-dev build-essential libgstreamer1.0-dev cmake-curses-gui ocl-icd-dev freeglut3-dev libgeotiff-dev libusb-1.0-0-dev libhdf5-dev libatlas-base-dev libjasper-dev libqtgui4 libqt4-test libglu1-mesa libdc1394-22-dev libtesseract-dev scons icu-devtools libjpeg-dev libpng-dev libtiff-dev libavcodec-dev libavformat-dev libswscale-dev libv4l-dev libxvidcore-dev libatlas-base-dev gfortran libopengl-dev git gcc xauth avahi-daemon x11-xserver-utils libopencv-dev python3-opencv unzip 

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
echo "DONE." >> $log
