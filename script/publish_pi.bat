@echo off
IF "%2"=="-j" GOTO JS
IF "%2"=="-c" GOTO CSS
IF NOT "%2"=="-s" GOTO BUILD
echo Stopping services
REM plink -no-antispoof -pw glimmrtv glimmrtv@%1 "echo glimmrtv | sudo -S pkill -f Glimmr"
plink -no-antispoof -pw glimmrtv glimmrtv@%1 "echo glimmrtv | sudo -S service glimmr stop"

:BUILD
IF NOT "%2"=="-k" GOTO BUILD2
echo Killing Glimmr task...
plink -no-antispoof -pw glimmrtv glimmrtv@%1 "echo glimmrtv | sudo -S pkill -f Glimmr"

:BUILD2
dotnet publish ..\src\Glimmr\Glimmr.csproj /p:PublishProfile=LinuxARM -o ..\src\Glimmr\bin\linuxARM
cd ..\src\Glimmr\bin\linuxARM

echo Copying new files...

pscp -P 22 -r -pw glimmrtv .\Glimmr.Views.dll glimmrtv@%1:/home/glimmrtv/glimmr/bin/Glimmr.Views.dll
pscp -P 22 -r -pw glimmrtv .\Glimmr.deps.json glimmrtv@%1:/home/glimmrtv/glimmr/bin/Glimmr.deps.json
pscp -P 22 -r -pw glimmrtv .\Glimmr.Views.pdb glimmrtv@%1:/home/glimmrtv/glimmr/bin/Glimmr.Views.pdb
pscp -P 22 -r -pw glimmrtv .\wwwroot\js\* glimmrtv@%1:/home/glimmrtv/glimmr/bin/wwwroot/js/
pscp -P 22 -r -pw glimmrtv .\wwwroot\css\* glimmrtv@%1:/home/glimmrtv/glimmr/bin/wwwroot/css/
pscp -P 22 -r -pw glimmrtv .\Glimmr.dll glimmrtv@%1:/home/glimmrtv/glimmr/bin/Glimmr.dll
pscp -P 22 -r -pw glimmrtv .\Glimmr.pdb glimmrtv@%1:/home/glimmrtv/glimmr/bin/Glimmr.pdb
pscp -P 22 -r -pw glimmrtv .\Glimmr.runtimeconfig.json glimmrtv@%1:/home/glimmrtv/glimmr/bin/Glimmr.runtimeconfig.json
pscp -P 22 -r -pw glimmrtv .\Glimmr glimmrtv@%1:/home/glimmrtv/glimmr/bin/Glimmr
IF NOT "%2"=="-s" GOTO END

echo Restarting glimmr...
plink -no-antispoof -pw glimmrtv glimmrtv@%1 "echo glimmrtv | sudo -S service glimmr start"

:END
cd ../../../..
