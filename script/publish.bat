@echo off
IF [%1] == [] GOTO Error
set version=%~1
dotnet build Glimmr.csproj /p:DeployOnBuild=true /p:PublishProfile=Linux
dotnet build Glimmr.csproj /p:DeployOnBuild=true /p:PublishProfile=LinuxARM
dotnet build Glimmr.csproj /p:DeployOnBuild=true /p:PublishProfile=Windows
dotnet build Glimmr.csproj /p:DeployOnBuild=true /p:PublishProfile=WindowsARM
dotnet build Glimmr.csproj /p:DeployOnBuild=true /p:PublishProfile=OSX

cd .\bin\publish
7z a -ttar -so -an -r .\Glimmr-linux\* | 7z a -si Glimmr-linux-%version%.tgz
7z a -ttar -so -an -r .\Glimmr-linux-arm\* | 7z a -si Glimmr-linux-arm-%version%.tgz
7z a -tzip -r Glimmr-windows-%version%.zip .\Glimmr-windows\*
7z a -tzip -r Glimmr-windows-arm-%version%.zip .\Glimmr-windows-arm\*
7z a -tzip -r Glimmr-osx-%version%.zip .\Glimmr-osx\*
GOTO End

:Error
echo Please enter a version number.

:End