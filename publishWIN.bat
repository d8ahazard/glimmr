@echo off
set version=1.1.0
dotnet build HueDream.csproj /p:DeployOnBuild=true /p:PublishProfile=Windows
