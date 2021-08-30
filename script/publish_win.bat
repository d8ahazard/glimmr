@echo off
dotnet publish -c release ..\src\Glimmr\Glimmr.csproj /p:PublishProfile=Windows -o ..\src\Glimmr\bin\Windows