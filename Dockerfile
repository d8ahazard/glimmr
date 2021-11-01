#Use Ubuntu Focal as the base image and add user glimmrtv + add it to the video group
From ubuntu:20.04 AS base
ENV DEBIAN_FRONTEND noninteractive
RUN useradd -ms /bin/bash glimmrtv
RUN usermod -aG sudo glimmrtv
RUN usermod -aG video glimmrtv

#Install Required Packages
RUN apt-get update && apt-get install -y software-properties-common
RUN apt-get install -y \
  sudo \
  curl \
  wget
RUN echo '%sudo ALL=(ALL) NOPASSWD:ALL' >> /etc/sudoers

#Install locally defined required packages
COPY linux-docker-packages.sh .
RUN chmod 777 linux-docker-packages.sh
RUN mkdir /opt/dotnet
RUN ./linux-docker-packages.sh

#Clean it up
RUN apt-get autoclean -y \
  && apt-get autoremove -y \
  && rm -rf /var/lib/apt/lists/*

#Finish up the good stuff
FROM base AS final
WORKDIR /etc/Glimmr/log
RUN ln -sf /etc/Glimmr/log /var/log/glimmr
WORKDIR /app
COPY --from=build /app/publish .
USER glimmrtv
ENV ASPNETCORE_URLS=http://+:5699
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
