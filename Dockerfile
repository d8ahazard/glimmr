FROM mcr.microsoft.com/dotnet/core/aspnet:3.0-buster-slim AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/core/sdk:3.0-buster AS build
WORKDIR /src
COPY ["HueDream.csproj", ""]
RUN dotnet restore "./HueDream.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "HueDream.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HueDream.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HueDream.dll"]