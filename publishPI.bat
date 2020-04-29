@echo off
REM plink -no-antispoof -pw raspberry root@192.168.1.38 service glimmr stop
set version=1.1.0
dotnet build HueDream.csproj /p:DeployOnBuild=true /p:PublishProfile=LinuxARM
cd .\bin\publish\huedream-linux-arm
echo Copying new files...
pscp -r -pw raspberry .\* pi@192.168.1.38:/home/pi/glimmr/
REM plink -no-antispoof -pw raspberry root@192.168.1.38 service glimmr start

