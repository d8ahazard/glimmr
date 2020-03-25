#/bin/bash
mkdir /home/glimmr
cd /home/glimmr
mkdir ./glimmr
cd ./glimmr
# Install Glimmr here

tar -xvf ./huedream-linux-arm-test.tgz -C ./glimmr
# Install dotnet
cd /home/glimmr
wget https://download.visualstudio.microsoft.com/download/pr/349f13f0-400e-476c-ba10-fe284b35b932/44a5863469051c5cf103129f1423ddb8/dotnet-sdk-3.1.102-linux-arm.tar.gz
wget https://download.visualstudio.microsoft.com/download/pr/8ccacf09-e5eb-481b-a407-2398b08ac6ac/1cef921566cb9d1ca8c742c9c26a521c/aspnetcore-runtime-3.1.2-linux-arm.tar.gz
mkdir -p /opt/dotnet
tar zxf dotnet-sdk-3.1.102-linux-arm.tar.gz -C /opt/dotnet
tar zxf ./aspnetcore-runtime-3.1.2-linux-arm.tar.gz -C /opt/dotnet
sudo ln -s /opt/dotnet/dotnet /usr/local/bin
# Cleanup
rm -rf ./dotnet-sdk-3.1.102-linux-arm.tar.gz
rm -rf ./aspnetcore-runtime-3.1.2-linux-arm.tar.gz
# Install dependencies
sudo apt-get -y install libgtk-3-dev libhdf5-dev libatlas-base-dev libjasper-dev libqtgui4 libqt4-test libglu1-mesa libdc1394-22 libtesseract-dev scons icu-devtools libjpeg-dev libpng-dev libtiff-dev libavcodec-dev libavformat-dev libswscale-dev libv4l-dev libxvidcore-dev libatlas-base-dev gfortran libopengl-dev git gcc xauth
# Cleanup
sudo apt-get -y remove x264 libx264-dev	
git clone https://github.com/jgarff/rpi_ws281x /home/glimmr/ws281x
cd /home/glimmr/ws281x
scons
echo "blacklist snd_bcm2835" > /etc/modprobe.d/snd-blacklist.conf
echo "spidev.bufsiz=32768" >> /boot/cmdline.txt
# Disable if not pi3
echo "core_freq=250" >> /boot/cmdline.txt
reboot
