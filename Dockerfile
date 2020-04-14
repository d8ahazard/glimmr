FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base
ENV ASPNETCORE_URLS=http://+:5699
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["HueDream.csproj", ""]
COPY ["pkg", "/root/pkg"]
COPY ["NuGet.Config", "~/.nuget/packages"]
RUN dotnet restore --source "/root/pkg" --source "https://api.nuget.org/v3/index.json" --source "https://www.myget.org/F/mmalsharp/api/v3/index.json"
RUN dotnet restore "./HueDream.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "HueDream.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HueDream.csproj" -c Release -o /app/publish
RUN mkdir -p /etc/huedream
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Glimmr.dll"]

VOLUME /etc/huedream
EXPOSE 1900/udp
EXPOSE 2100/udp
EXPOSE 5699
EXPOSE 8888/udp
