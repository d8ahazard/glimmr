#Use Ubuntu Focal as the base image and add user glimmrtv + add it to the video group
From ubuntu:20.04 AS base
ENV DEBIAN_FRONTEND noninteractive

# Update again
RUN apt-get -y update && apt-get upgrade
# Install packages
RUN apt-get install -y \
  sudo curl wget libgtk-3-dev libhdf5-dev libatlas-base-dev libglu1-mesa \
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
ENTRYPOINT ["sudo", "/usr/share/Glimmr/Glimmr"]
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
