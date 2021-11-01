#Use Ubuntu Focal as the base image and add user glimmrtv + add it to the video group
From ubuntu:20.04 AS base
ENV DEBIAN_FRONTEND noninteractive
RUN useradd -ms /bin/bash glimmrtv
RUN usermod -aG sudo glimmrtv
RUN usermod -aG video glimmrtv

#Update so we get software-properties-common
RUN apt-get -y update
RUN apt-get install -y software-properties-common
# Add extra repositories 
RUN add-apt-repository "deb http://security.ubuntu.com/ubuntu xenial-security main"
RUN add-apt-repository ppa:rock-core/qt4 -y
# Update again
RUN apt-get -y update
# Install packages
RUN apt-get install -y \
  sudo curl wget libgtk-3-dev libhdf5-dev libatlas-base-dev libjasper-dev libqtgui4 libqt4-test libglu1-mesa \
  libdc1394-22 libtesseract-dev scons icu-devtools libjpeg-dev libpng-dev libtiff-dev libavcodec-dev libavformat-dev \
  libswscale-dev libv4l-dev libxvidcore-dev libatlas-base-dev gfortran libopengl-dev git gcc xauth avahi-daemon \
  x11-xserver-utils libopencv-dev python3-opencv unzip libtiff5-dev libgeotiff-dev libgtk-3-dev libgstreamer1.0-dev \
  libavcodec-dev libswscale-dev libavformat-dev libopenexr-dev libjasper-dev libdc1394-22-dev libv4l-dev \
  libeigen3-dev libopengl-dev cmake-curses-gui freeglut3-dev lm-sensors
  
RUN echo '%sudo ALL=(ALL) NOPASSWD:ALL' >> /etc/sudoers

#Install glimmr
COPY docker_install.sh .
RUN chmod 777 docker_install.sh
RUN ./docker_install.sh

#Clean it up
RUN apt-get autoclean -y \
  && apt-get autoremove -y \
  && rm -rf /var/lib/apt/lists/*

#Finish up the good stuff
FROM base AS final
WORKDIR /etc/Glimmr/log
RUN ln -sf /etc/Glimmr/log /var/log/glimmr
USER glimmrtv
ENV ASPNETCORE_URLS=http://+:5699
WORKDIR /opt/glimmr
ENTRYPOINT ["sudo", "/opt/glimmr/Glimmr"]
VOLUME /etc/Glimmr 
# Web UI
EXPOSE 5699
EXPOSE 5670
# Hue Discovery
EXPOSE 1900/udp
# Hue Streaming
EXPOSE 2100/udp
# MDNS Discovery
EXPOSE 5353/udp
# Dreamscreen
EXPOSE 8888/udp
# Lifx
EXPOSE 56700/udp
# Nanoleaf
EXPOSE 60222/udp
