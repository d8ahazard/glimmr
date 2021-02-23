@echo off
set version=1.1.2
dotnet publish ..\src\Glimmr.csproj /p:PublishProfile=Windows -o ..\src\bin\windows