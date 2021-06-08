#!/bin/bash

export DEBIAN_FRONTEND=noninteractive

# Add extra repos for users on Ubuntu 20- for libjasper-dev
add-apt-repository "deb http://security.ubuntu.com/ubuntu xenial-security main"

# Add extra repos for users on Ubuntu 20- for libqtgui4 libqt4-test
add-apt-repository ppa:rock-core/qt4 -y
apt-get -y update && apt-get -y upgrade

apt-get -y install libgtk-3-dev
apt-get -y install build-essential
apt-get -y install libgstreamer1.0-dev
apt-get -y install cmake-curses-gui
apt-get -y install ocl-icd-dev
apt-get -y install freeglut3-dev
apt-get -y install libgeotiff-dev
apt-get -y install libusb-1.0-0-dev
apt-get -y install libhdf5-dev
apt-get -y install libatlas-base-dev
apt-get -y install libjasper-dev
apt-get -y install libqtgui4
apt-get -y install libqt4-test
apt-get -y install libglu1-mesa
apt-get -y install libdc1394-22-dev
apt-get -y install libtesseract-dev
apt-get -y install scons
apt-get -y install icu-devtools
apt-get -y install libjpeg-dev
apt-get -y install libpng-dev
apt-get -y install libtiff-dev
apt-get -y install libavcodec-dev
apt-get -y install libavformat-dev
apt-get -y install libswscale-dev
apt-get -y install libv4l-dev
apt-get -y install libxvidcore-dev
apt-get -y install libatlas-base-dev
apt-get -y install gfortran
apt-get -y install libopengl-dev
apt-get -y install git
apt-get -y install gcc
apt-get -y install xauth
apt-get -y install avahi-daemon
apt-get -y install x11-xserver-utils
apt-get -y install libopencv-dev
apt-get -y install python3-opencv
apt-get -y install unzip
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel Current --install-dir /opt/dotnet
