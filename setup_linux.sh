#!/bin/bash
# Add user if not exist
id -u glimmrtv &>/dev/null || useradd -m glimmrtv 
cd /home/glimmrtv || exit
# Check dotnet installation
if [ ! -d "/opt/dotnet" ]
then 
  echo "Installing dotnet."
  echo "A..."
  wget https://download.visualstudio.microsoft.com/download/pr/daec2daf-b458-4ae1-9046-b8ba09b5fb49/733e2d73b41640d6e6bdf1cc6b9ef03b/dotnet-sdk-3.1.200-linux-x64.tar.gz
  wget https://download.visualstudio.microsoft.com/download/pr/2d72ee67-ac4d-42c6-97d9-a26a28201fc8/977ad14b99b6ed03dcefd6655789e43a/aspnetcore-runtime-3.1.2-linux-x64.tar.gz
  mkdir -p /opt/dotnet
  echo "Extracting Dotnet-SDK..."
  tar zxf dotnet-sdk-3.1.200-linux-x64.tar.gz -C /opt/dotnet
  echo "DONE!"
  echo "Extracting runtime..."
  tar zxf ./aspnetcore-runtime-3.1.2-linux-x64.tar.gz -C /opt/dotnet
  echo "DONE!"
  echo "Symlinking..."
  sudo ln -s /opt/dotnet/dotnet /usr/local/bin
  echo "DONE!"
  # Cleanup
  echo "Cleanup..."
  rm -rf ./dotnet-sdk-3.1.102-linux-arm.tar.gz
  rm -rf ./aspnetcore-runtime-3.1.2-linux-arm.tar.gz
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

if [ ! -d "/home/glimmrtv/glimmr" ]
then
# Clone glimmr
  echo "Cloning glimmr"
  git clone https://github.com/d8ahazard/glimmr /home/glimmrtv/glimmr/src
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
read -n 1 -r -s -p $'Install complete, press enter to continue. You may want to reboot your system.\n'
