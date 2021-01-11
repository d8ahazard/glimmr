#!/bin/bash
# Add user if not exist
id -u glimmrtv &>/dev/null || useradd -m glimmrtv 
cd /home/glimmrtv || exit
# Check dotnet installation
if [ ! -d "/opt/dotnet" ]
then 
  echo "Installing dotnet."
  echo "Downloading..."
  wget https://dotnetcli.blob.core.windows.net/dotnet/Sdk/master/dotnet-sdk-latest-linux.tar.gz
  sudo mkdir -p /usr/share/dotnet
  sudo tar -zxf dotnet-sdk-latest-linux.tar.gz -C /usr/share/dotnet
  sudo ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet
  echo "DONE!"
  # Cleanup
  echo "Cleanup..."
  rm -rf ./dotnet-sdk-latest-linux-arm.tar.gz
  echo "DONE!"
fi

cd /opt/dotnet || exit
# Install dependencies
echo "Installing dependencies..."
sudo apt-get -y update && apt-get -y upgrade
sudo apt-get -y install libgtk-3-dev libhdf5-dev libatlas-base-dev libjasper-dev libqtgui4 libqt4-test libglu1-mesa libdc1394-22 libtesseract-dev scons icu-devtools libjpeg-dev libpng-dev libtiff-dev libavcodec-dev libavformat-dev libswscale-dev libv4l-dev libxvidcore-dev libatlas-base-dev gfortran libopengl-dev git gcc xauth
echo "DONE!"
# Moar Cleanup
echo "More cleanup..."
sudo apt-get -y remove x264 libx264-dev

if [ ! -d "/home/glimmrtv/glimmr" ]
then
# Clone glimmr
  echo "Cloning glimmr"
  git clone -b dev https://github.com/d8ahazard/glimmr /home/glimmrtv/glimmr/src
else
  echo "Source exists, updating..."
  cd /home/glimmrtv/glimmr/src || exit
  git stash && git fetch && git pull
fi

cd /home/glimmrtv/glimmr/src || exit

# Check for service stop
serviceName="glimmr"
if systemctl --all --type service | grep -q "$serviceName";then
  echo "Stopping glimmr..."
    service glimmr stop
    echo "STOPPED!"
else
    echo "$serviceName is not installed."
fi

# Build latest version
echo "Building glimmr..."
dotnet build Glimmr.csproj /p:PublishProfile=LinuxARM
cp -r /home/glimmrtv/glimmr/src/bin/debug/netcoreapp3.1/linux-arm/* /home/glimmrtv/glimmr/
cp -r /home/glimmrtv/glimmr/src/wwwroot/ /home/glimmrtv/glimmr/wwwroot/
echo "DONE."
# Copy necessary libraries
echo "Copying libs..."
cp -r /home/glimmrtv/glimmr/src/build/arm /usr/lib

# Check service start/install
if systemctl --all --type service | grep -q "$serviceName";then
  echo "Starting glimmr..."
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
WorkingDirectory=/home/glimmrtv/glimmr
ExecStart=/home/glimmrtv/glimmr/Glimmr


[Install]
WantedBy=multi-user.target

" > /etc/systemd/system/glimmr.service
  systemctl daemon-reload
  systemctl enable glimmr.service
  systemctl start glimmr.service
fi
read -n 1 -r -s -p $'Install complete, press enter to continue. You may want to reboot now.\n'