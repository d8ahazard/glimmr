#!/bin/bash

PUBPROFILE="Linux";
if [ -f "/usr/bin/raspi-config" ] 
  then
    PUBPROFILE="LinuxARM"
fi

unameOut="$(uname -s)"
if [ $unameOut = "Darwin" ]
  then
    PUBPROFILE="OSX"
fi

if [ $unameOut = "FreeBSD" ]
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

# Check dotnet installation
if [ ! -f "/opt/dotnet/dotnet" ]
then 
  echo "Installing dotnet."
  echo "Downloading..."
  curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel Current --install-dir /opt/dotnet
  echo "DONE!"  
fi

cd /opt/dotnet || exit

if [ ! -f "/home/glimmrtv/firstrun" ]
then
  echo "Starting first-run setup..."
  if [ $PUBPROFILE = "LinuxARM" ] 
  then
    echo "Installing Linux-ARM dependencies..."
    # Install dependencies
    sudo apt-get -y update && apt-get -y upgrade
    sudo apt-get -y install libgtk-3-dev libhdf5-dev libatlas-base-dev libjasper-dev libqtgui4 libqt4-test libglu1-mesa libdc1394-22 libtesseract-dev scons icu-devtools libjpeg-dev libpng-dev libtiff-dev libavcodec-dev libavformat-dev libswscale-dev libv4l-dev libxvidcore-dev libatlas-base-dev gfortran libopengl-dev git gcc xauth avahi-daemon x11-xserver-utils
    
    #Assign existing hostname to $hostn
    hostn=$(cat /etc/hostname)
    hostn=`cat /etc/hostname | tr -d " \t\n\r"`
    serial=$(cat /proc/cpuinfo | grep -E "^Serial" | sed "s/.*: 0*//")
    newhost=glimmr-"${serial: -4}"
    if [ "$hostn" != $newhost ]; then
      echo "Changing hostname from $hostn to $newhost..." >&2
      echo $newhost > /etc/hostname
      sudo sed -i "s/127.0.1.1.*$CURRENT_HOSTNAME\$/127.0.1.1\t$NEW_HOSTNAME/g" /etc/hosts
    fi
    
    if [ ! -d "/home/glimmrtv/ws281x" ]
    then
      echo "Cloning LED libraries..."	
      git clone https://github.com/jgarff/rpi_ws281x /home/glimmrtv/ws281x
      cd /home/glimmrtv/ws281x || exit
      echo "LED setup..."
      scons
      gcc -shared -o ws2811.so *.o
      cp ./ws2811.so /usr/lib/ws2811.so
      echo "DONE!"
    fi
    echo "Raspi first-config is done!"
  fi

  if [ $PUBPROFILE = "Linux" ] 
  then
      echo "Installing Linux (x64) dependencies..."
      # Add extra repos for users on Ubuntu 20- for libjasper-dev
      sudo add-apt-repository "deb http://security.ubuntu.com/ubuntu xenial-security main"
      # Add extra repos for users on Ubuntu 20- for libqtgui4 libqt4-test
      sudo add-apt-repository ppa:rock-core/qt4 -y
      sudo apt-get -y update && apt-get -y upgrade
      sudo apt-get -y install libgtk-3-dev libhdf5-dev libatlas-base-dev libjasper-dev libqtgui4 libqt4-test libglu1-mesa libdc1394-22 libtesseract-dev scons icu-devtools libjpeg-dev libpng-dev libtiff-dev libavcodec-dev libavformat-dev libswscale-dev libv4l-dev libxvidcore-dev libatlas-base-dev gfortran libopengl-dev git gcc xauth avahi-daemon x11-xserver-utils libopencv-dev python3-opencv unzip
      echo "DONE!"
  fi
  if [ $PUBPROFILE = "OSX" ]
  then
    echo "Hey there, I see you're trying to run this on OSX. You should leave a comment in the github issues section so we can fill this out!"
  fi
  echo "done" > "/home/glimmrtv/firstrun"
fi


if [ ! -d "/home/glimmrtv/glimmr" ]
then
# Clone glimmr
  echo "Cloning glimmr"
  git clone -b dev https://github.com/d8ahazard/glimmr /home/glimmrtv/glimmr
  # Install update script to init.d   
  sudo cp /home/glimmrtv/glimmr/script/update_linux.sh /etc/init.d/update_linux.sh
  sudo chmod 777 /etc/init.d/update_linux.sh
else
  echo "Source exists, updating..."
  cd /home/glimmrtv/glimmr || exit
  git fetch && git pull
fi

cd /home/glimmrtv/glimmr || exit

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
/opt/dotnet/dotnet publish /home/glimmrtv/glimmr/src/Glimmr.csproj /p:PublishProfile=$PUBPROFILE -o /home/glimmrtv/glimmr/bin/
echo "DONE."
chmod -R 777 /home/glimmrtv/glimmr/bin

#Get libcvextern.so
if [ $PUBPROFILE = "Linux" ]
then
  echo "Installing libs for emgu.cv (Linux x64)"
  sudo mkdir /home/glimmrtv/glimmr/lib/linux/libcvextern
  cd /home/glimmrtv/glimmr/lib/linux/libcvextern || exit
  sudo wget https://www.nuget.org/api/v2/package/Emgu.CV.runtime.ubuntu.20.04-x64/4.5.1.4349
  unzip 4.5.1.4349
  sudo cp /home/glimmrtv/glimmr/lib/linux/libcvextern/runtimes/ubuntu.20.04-x64/native/libcvextern.so /home/glimmrtv/glimmr/lib/Linux/libcvextern.so
  sudo rm -r /home/glimmrtv/glimmr/lib/linux/libcvextern
fi

if [ $PUBPROFILE = "LinuxARM" ]
then
  echo "Installing libs for emgu.cv (Linux x64)"
  sudo mkdir /home/glimmrtv/glimmr/lib/LinuxARM/libcvextern
  cd /home/glimmrtv/glimmr/lib/LinuxARM/libcvextern || exit
  sudo wget https://www.nuget.org/api/v2/package/Emgu.CV.runtime.linux-arm/4.5.1.4349
  unzip 4.5.1.4349
  sudo cp /home/glimmrtv/glimmr/lib/LinuxARM/libcvextern/runtimes/linux-arm/native/libcvextern.so /home/glimmrtv/glimmr/lib/LinuxARM/libcvextern.so
  sudo rm -r /home/glimmrtv/glimmr/lib/LinuxARM/libcvextern
fi

# Copy necessary libraries
echo "Copying libs..."
cp -r /home/glimmrtv/glimmr/lib/bass.dll /usr/lib/bass.dll
if [ -d "/home/glimmrtv/glimmr/lib/$PUBPROFILE" ]
then
  cp -r /home/glimmrtv/glimmr/lib/$PUBPROFILE/* /usr/lib
fi

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
WorkingDirectory=/home/glimmrtv/glimmr/bin
ExecStart=sudo /opt/dotnet/dotnet /home/glimmrtv/glimmr/bin/Glimmr.dll
KillMode=process

[Install]
WantedBy=multi-user.target

" > /etc/systemd/system/glimmr.service
  systemctl daemon-reload
  systemctl enable glimmr.service
  systemctl start glimmr.service
fi
read -n 1 -r -s -p $'Install complete, press enter to continue. You may want to reboot now.\n'
