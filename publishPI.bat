@echo off

set version=1.1.0
dotnet build HueDream.csproj /p:DeployOnBuild=true /p:PublishProfile=LinuxARM

cd .\bin\publish
del huedream-linux-arm-test.tgz
7z a -ttar -so -an -r .\huedream-linux-arm\* | 7z a -si huedream-linux-arm-test.tgz
