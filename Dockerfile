FROM mcr.microsoft.com/dotnet/core/aspnet:3.0-buster-slim AS base
ENV ASPNETCORE_URLS=http://+:5666
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.0-buster AS build
WORKDIR /src
COPY ["HueDream.csproj", ""]
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
ENTRYPOINT ["dotnet", "HueDream.dll"]

VOLUME /etc/huedream
EXPOSE 1900/udp
EXPOSE 2100/udp
EXPOSE 5666
EXPOSE 8888/udp
