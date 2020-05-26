#!/bin/bash
# Add user if not exist
id -u glimmrtv &>/dev/null || useradd -m glimmrtv 
cd /home/glimmrtv || exit
# Check dotnet installation
if [ ! -d "/opt/dotnet" ]
then 
  wget https://download.visualstudio.microsoft.com/download/pr/349f13f0-400e-476c-ba10-fe284b35b932/44a5863469051c5cf103129f1423ddb8/dotnet-sdk-3.1.102-linux-arm.tar.gz
  wget https://download.visualstudio.microsoft.com/download/pr/8ccacf09-e5eb-481b-a407-2398b08ac6ac/1cef921566cb9d1ca8c742c9c26a521c/aspnetcore-runtime-3.1.2-linux-arm.tar.gz
  mkdir -p /opt/dotnet
  tar zxf dotnet-sdk-3.1.102-linux-arm.tar.gz -C /opt/dotnet
  tar zxf ./aspnetcore-runtime-3.1.2-linux-arm.tar.gz -C /opt/dotnet
  sudo ln -s /opt/dotnet/dotnet /usr/local/bin
  # Cleanup
  rm -rf ./dotnet-sdk-3.1.102-linux-arm.tar.gz
  rm -rf ./aspnetcore-runtime-3.1.2-linux-arm.tar.gz
fi

# Install dependencies
sudo apt-get -y install libgtk-3-dev libhdf5-dev libatlas-base-dev libjasper-dev libqtgui4 libqt4-test libglu1-mesa libdc1394-22 libtesseract-dev scons icu-devtools libjpeg-dev libpng-dev libtiff-dev libavcodec-dev libavformat-dev libswscale-dev libv4l-dev libxvidcore-dev libatlas-base-dev gfortran libopengl-dev git gcc xauth
# Moar Cleanup
sudo apt-get -y remove x264 libx264-dev	
git clone https://github.com/jgarff/rpi_ws281x /home/glimmrtv/ws281x
cd /home/glimmrtv/ws281x || exit
scons
if [ ! -d "/home/glimmrtv/glimmr" ]
then
# Clone glimmr
  git clone https://github.com/d8ahazard/glimmr /home/glimmrtv/glimmr/src
else
  cd /home/glimmrtv/glimmr/src || exit
  git stash && git fetch && git pull
fi
cd /home/glimmrtv/glimmr/src || exit

# Check for service stop
serviceName="glimmr"
if systemctl --all --type service | grep -q "$serviceName";then
    service glimmr stop
else
    echo "$serviceName is not installed."
fi

# Build latest version
dotnet build HueDream.csproj /p:PublishProfile=LinuxARM -o /home/glimmrtv/glimmr
# Copy necessary libraries
cp -r /home/glimmrtv/glimmr/src/build/arm /usr/lib

# Check service start/install
if systemctl --all --type service | grep -q "$serviceName";then
    service glimmr start
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
WorkingDirectory=/home/glimmr/glimmr
ExecStart=/home/glimmr/glimmr/Glimmr


[Install]
WantedBy=multi-user.target

" >> /etc/systemd/system/glimmr.service
  systemctl daemon-reload
  systemctl enable glimmr.service
  systemctl start glimmr.service
fi

reboot
