@echo off
IF [%1] == [] GOTO Error
set version=%~1
dotnet build HueDream.csproj /p:DeployOnBuild=true /p:PublishProfile=Linux
dotnet build HueDream.csproj /p:DeployOnBuild=true /p:PublishProfile=LinuxARM
dotnet build HueDream.csproj /p:DeployOnBuild=true /p:PublishProfile=Windows
dotnet build HueDream.csproj /p:DeployOnBuild=true /p:PublishProfile=WindowsARM
dotnet build HueDream.csproj /p:DeployOnBuild=true /p:PublishProfile=OSX

cd .\bin\publish
7z a -ttar -so -an -r .\huedream-linux\* | 7z a -si huedream-linux-%version%.tgz
7z a -ttar -so -an -r .\huedream-linux-arm\* | 7z a -si huedream-linux-arm-%version%.tgz
7z a -tzip -r huedream-windows-%version%.zip .\huedream-windows\*
7z a -tzip -r huedream-windows-arm-%version%.zip .\huedream-windows-arm\*
7z a -tzip -r huedream-osx-%version%.zip .\huedream-osx\*
GOTO End

:Error
echo Please enter a version number.

:End