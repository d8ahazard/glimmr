#Use Ubuntu Focal as the base image and add user glimmrtv + add it to the video group
From ubuntu:20.04 AS base
ENV DEBIAN_FRONTEND noninteractive
RUN useradd -ms /bin/bash glimmrtv
WORKDIR /home/glimmrtv
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
 
#Create directory for libcvextern to download into / be unziped in
WORKDIR /home/glimmrtv/linux-libcvextern
RUN wget https://www.nuget.org/api/v2/package/Emgu.CV.runtime.ubuntu.20.04-x64/4.5.1.4349
RUN unzip 4.5.1.4349
RUN mkdir -p /root/pkg
RUN wget https://www.nuget.org/api/v2/package/Emgu.CV.runtime.ubuntu.20.04-x64/4.5.1.4349 -O /root/pkg/emgu.cv.runtime.ubuntu.20.04-x64.4.5.1.4349.nupkg

WORKDIR ./runtimes/ubuntu.20.04-x64/native
RUN cp libcvextern.so /root/
RUN cp libcvextern.so /usr/lib/

#Remove downloaded libcvextern directory/package once it is copied over
WORKDIR /home/glimmrtv
RUN rm -r linux-libcvextern

#Copy over all files from repo to /glimmr directory in image + copy over linux lib files
WORKDIR /home/glimmrtv/glimmr
COPY . .
COPY ["pkg", "/root/pkg"]
COPY ["/lib/Linux", "/root/"]
COPY ["/lib/Linux", "/usr/lib/"]
COPY ["./NuGet.Config", "~/.nuget/packages"]

WORKDIR /app

FROM base as build
WORKDIR /home/glimmrtv/glimmr
COPY --from=base /home/glimmrtv/glimmr .

WORKDIR /home/glimmrtv/glimmr/src

RUN /opt/dotnet/dotnet restore --source "https://api.nuget.org/v3/index.json" --source "https://www.myget.org/F/mmalsharp/api/v3/index.json"
RUN /opt/dotnet/dotnet restore "Glimmr.csproj"
RUN /opt/dotnet/dotnet build "Glimmr.csproj" -c Release /p:PublishProfile=Linux -o /app/build
RUN /opt/dotnet/dotnet publish "Glimmr.csproj" -c Release /p:PublishProfile=Linux -o /app/publish

#Finish up the good stuff
FROM base AS final
WORKDIR /etc/Glimmr/log
RUN ln -sf /etc/Glimmr/log /var/log/glimmr
WORKDIR /app
COPY --from=build /app/publish .
USER glimmrtv
ENV ASPNETCORE_URLS=http://+:5699
ENTRYPOINT ["sudo", "/opt/dotnet/dotnet", "Glimmr.dll"]
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
