#!/bin/bash
PUBPROFILE="Linux";
PUBFILE="linux"
if [ -f "/usr/bin/raspi-config" ] 
  then
    PUBPROFILE="LinuxARM"
    PUBFILE="linux-arm"
fi

unameOut="$(uname -s)"
if [ "$unameOut" == "Darwin" ]
  then
    echo "Please use the setup_osx.sh script for OSX installation."
    exit 0
fi

if [ "$unameOut" == "FreeBSD" ]
  then
    PUBPROFILE="Portable"
fi

echo "Beginning Glimmr setup/update for $PUBPROFILE"

# Add user if not exist, set up necessary groups
id -u glimmrtv &>/dev/null || useradd -m glimmrtv
usermod -aG sudo glimmrtv 
usermod -aG video glimmrtv
usermod -aG video $USER
cd /home/glimmrtv || exit

if [ ! -f "/usr/share/Glimmr/firstrun" ]
then
  echo "Starting first-run setup..."
  if [ "$PUBPROFILE" == "LinuxARM" ] 
  then
    echo "Installing Linux-ARM dependencies..."
    # Install dependencies
    sudo apt-get -y update && apt-get -y upgrade
    sudo apt-get -y install libgtk-3-dev libhdf5-dev libatlas-base-dev libjasper-dev libqtgui4 libqt4-test libglu1-mesa libdc1394-22 libtesseract-dev icu-devtools libjpeg-dev libpng-dev libtiff-dev libavcodec-dev libavformat-dev libswscale-dev libv4l-dev libxvidcore-dev gfortran libopengl-dev git gcc xauth avahi-daemon x11-xserver-utils libtiff5-dev libgeotiff-dev libgtk-3-dev libgstreamer1.0-dev libavcodec-dev libswscale-dev libavformat-dev libopenexr-dev libjasper-dev libdc1394-22-dev libv4l-dev libeigen3-dev libopengl-dev cmake-curses-gui freeglut3-dev
    echo "gpio=19=op,a5" >> /boot/config.txt    
    echo "Raspi first-config is done!"
  fi

  if [ "$PUBPROFILE" == "Linux" ] 
  then
      echo "Installing Linux (x64) dependencies..."
      # Add extra repos for users on Ubuntu 20- for libjasper-dev, libqtgui4 libqt4-test 
      sudo add-apt-repository "deb http://security.ubuntu.com/ubuntu xenial-security main"
      sudo add-apt-repository ppa:rock-core/qt4 -y
      sudo apt-get -y update && apt-get -y upgrade
      sudo apt-get -y install libgtk-3-dev libhdf5-dev libatlas-base-dev libjasper-dev libqtgui4 libqt4-test libglu1-mesa libdc1394-22 libtesseract-dev icu-devtools libjpeg-dev libpng-dev libtiff-dev libavcodec-dev libavformat-dev libswscale-dev libv4l-dev libxvidcore-dev libatlas-base-dev gfortran libopengl-dev git gcc xauth avahi-daemon x11-xserver-utils libopencv-dev python3-opencv unzip libtiff5-dev libgeotiff-dev libgtk-3-dev libgstreamer1.0-dev libavcodec-dev libswscale-dev libavformat-dev libopenexr-dev libjasper-dev libdc1394-22-dev libv4l-dev libeigen3-dev libopengl-dev cmake-curses-gui freeglut3-dev lm-sensors
      echo "DONE!"
  fi
  
  if [ ! -d "/usr/share/Glimmr" ]
    then
  # Make dir
    mkdir /usr/share/Glimmr  
  fi
  echo "done" > "/usr/share/Glimmr/firstrun"
fi

# Check for service stop
serviceName="glimmr"
if systemctl --all --type service | grep -q "$serviceName";then
  echo "Stopping glimmr..."
    service glimmr stop
    echo "STOPPED!"
else
    echo "$serviceName is not installed."
fi

# Download and extract latest release
cd /tmp || exit
ver=$(wget "https://api.github.com/repos/d8ahazard/glimmr/releases/latest" -q -O - | grep '"tag_name":' | sed -E 's/.*"([^"]+)".*/\1/')
echo ver is $ver
url="https://github.com/d8ahazard/glimmr/releases/download/$ver/Glimmr-$PUBPATH-$ver.tgz"
echo Grabbing archive from $url
wget -O archive.tgz $url
tar zxvf ./archive.tgz -C /usr/share/Glimmr/
chmod -R 777 /usr/share/Glimmr
rm ./archive.tgz
echo "DONE." >> $log

# Check service start/install
if systemctl --all --type service | grep -q "$serviceName";then
  echo "Starting Glimmr service..."
  systemctl daemon-reload
  service glimmr start
  echo "DONE!"
else  
  echo "$serviceName does NOT exist, installing."
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
ExecStart=sudo Glimmr
KillMode=process

[Install]
WantedBy=multi-user.target

" > /etc/systemd/system/glimmr.service
  systemctl daemon-reload
  systemctl enable glimmr.service
  systemctl start glimmr.service
fi
read -n 1 -r -s -p $'Install complete, press enter to continue.\n'