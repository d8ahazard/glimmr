@echo off
REM plink -no-antispoof -pw glimmr root@glimmr service glimmr stop
set version=1.1.0
dotnet build HueDream.csproj /p:DeployOnBuild=true /p:PublishProfile=LinuxARM
cd .\bin\publish\huedream-linux-arm
echo Copying new files...
pscp -pw glimmr .\Glimmr dietpi@glimmr:/home/dietpi/glimmr
pscp -r -pw glimmr .\wwwroot dietpi@glimmr:/home/dietpi/glimmr
pscp -pw glimmr .\Glimmr.deps.json dietpi@glimmr:/home/dietpi/glimmr
pscp -pw glimmr .\Glimmr.dll dietpi@glimmr:/home/dietpi/glimmr
pscp -pw glimmr .\Glimmr.pdb dietpi@glimmr:/home/dietpi/glimmr
pscp -pw glimmr .\Glimmr.runtimeconfig.json dietpi@glimmr:/home/dietpi/glimmr
pscp -pw glimmr .\Glimmr.Views.dll dietpi@glimmr:/home/dietpi/glimmr
pscp -pw glimmr .\Glimmr.Views.pdb dietpi@glimmr:/home/dietpi/glimmr
pscp -pw glimmr .\web.config dietpi@glimmr:/home/dietpi/glimmr
REM plink -no-antispoof -pw glimmr root@glimmr service glimmr start

