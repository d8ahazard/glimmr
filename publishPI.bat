@echo off

set version=1.1.0
dotnet build HueDream.csproj /p:DeployOnBuild=true /p:PublishProfile=LinuxARM
cd .\bin\publish\huedream-linux-arm
plink -no-antispoof -pw glimmr root@192.168.1.30 service glimmr stop
pscp -pw glimmr .\Glimmr dietpi@192.168.1.30:/home/dietpi/glimmr
pscp -pw glimmr .\Glimmr.deps.json dietpi@192.168.1.30:/home/dietpi/glimmr
pscp -pw glimmr .\Glimmr.dll dietpi@192.168.1.30:/home/dietpi/glimmr
pscp -pw glimmr .\Glimmr.pdb dietpi@192.168.1.30:/home/dietpi/glimmr
pscp -pw glimmr .\Glimmr.runtimeconfig.json dietpi@192.168.1.30:/home/dietpi/glimmr
pscp -pw glimmr .\Glimmr.Views.dll dietpi@192.168.1.30:/home/dietpi/glimmr
pscp -pw glimmr .\Glimmr.Views.pdb dietpi@192.168.1.30:/home/dietpi/glimmr
pscp -pw glimmr .\web.config dietpi@192.168.1.30:/home/dietpi/glimmr
plink -no-antispoof -pw glimmr root@192.168.1.30 service glimmr start
