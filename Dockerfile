#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0-focal-amd64 AS base
ENV ASPNETCORE_URLS=http://+:5699
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0-focal-amd64 AS build
COPY linux-docker-packages.sh .
RUN chmod 777 linux-docker-packages.sh
RUN DEBIAN_FRONTEND=noninteractive apt-get update && apt-get install -y software-properties-common
RUN DEBIAN_FRONTEND=noninteractive apt-get -y install wget
RUN ./linux-docker-packages.sh

#Create directory for libcvextern to download into / be unziped in
WORKDIR /linux-libcvextern
RUN wget https://www.nuget.org/api/v2/package/Emgu.CV.runtime.ubuntu.20.04-x64/4.5.1.4349
RUN unzip 4.5.1.4349
RUN mkdir -p /root/pkg
RUN wget https://www.nuget.org/api/v2/package/Emgu.CV.runtime.ubuntu.20.04-x64/4.5.1.4349 -O /root/pkg/emgu.cv.runtime.ubuntu.20.04-x64.4.5.1.4349.nupkg

WORKDIR ./runtimes/ubuntu.20.04-x64/native
RUN cp libcvextern.so /root/
RUN cp libcvextern.so /usr/lib/

#Remove downloaded libcvextern directory/package once it is copied over
WORKDIR /
RUN rm -r /linux-libcvextern

#Copy over all files from repo to /glimmr directory in image + copy over linux lib files
WORKDIR /glimmr
COPY . .
COPY ["pkg", "/root/pkg"]
COPY ["/lib/linux", "/root/"]
COPY ["/lib/linux", "/usr/lib/"]
COPY ["./NuGet.Config", "~/.nuget/packages"]

WORKDIR /glimmr/src
RUN dotnet restore --source "https://api.nuget.org/v3/index.json" --source "https://www.myget.org/F/mmalsharp/api/v3/index.json"
RUN dotnet restore "Glimmr.csproj"
RUN dotnet build "Glimmr.csproj" -c Release /p:PublishProfile=Linux -o /app/build

FROM build AS publish
RUN dotnet publish "Glimmr.csproj" -c Release /p:PublishProfile=Linux -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Glimmr.dll"]
VOLUME /etc/glimmr
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
