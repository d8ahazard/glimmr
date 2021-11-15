#Use Ubuntu Focal as the base image and add user glimmrtv + add it to the video group
From ubuntu:20.04 AS base

ENV DEBIAN_FRONTEND noninteractive
    
RUN echo '%sudo ALL=(ALL) NOPASSWD:ALL' >> /etc/sudoers

WORKDIR /usr/share
#Install glimmr using our normal script
COPY ./script/setup_linux.sh .
RUN chmod 777 setup_linux.sh
RUN ./setup_linux.sh docker; exit 0

#Finish up the good stuff
FROM base AS final
WORKDIR /etc/Glimmr/log
RUN ln -sf /etc/Glimmr/log /var/log/glimmr
USER root
ENV ASPNETCORE_URLS=http://+:5699
WORKDIR /usr/share/Glimmr/
ENTRYPOINT ["/usr/share/Glimmr/Glimmr"]
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
