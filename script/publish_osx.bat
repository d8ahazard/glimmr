@echo off
if "%1"=="-h" GOTO HELP
IF NOT "%2"=="-s" GOTO BUILD
echo Stopping Glimmr
plink -no-antispoof -pw intozoom intozoom@%1 "echo intozoom | sudo -S pkill -f Glimmr"

:BUILD
IF NOT "%2"=="-k" GOTO BUILD2
echo Killing Glimmr task...
plink -no-antispoof -pw intozoom intozoom@%1 "echo intozoom | sudo -S pkill -f Glimmr"

:BUILD2
dotnet publish ..\src\Glimmr\Glimmr.csproj /p:PublishProfile=OSX -o ..\src\Glimmr\bin\OSX --self-contained=true
if "%1"=="" GOTO :END
cd ..\src\Glimmr\bin\OSX
echo Copying OSX Files...
xcopy /Y /E .\* ..\..\..\..\..\Glimmr-macos-installer-builder\macOS-x64\application\
IF "%2"=="-j" GOTO JS
IF "%2"=="-c" GOTO CSS
IF "%2"=="-w" GOTO WEB

IF "%2"=="-f" GOTO FULL
echo Copying main files...
pscp -P 22 -r -pw intozoom .\Glimmr.Views.dll intozoom@%1:/Library/Glimmr/1.2.0/Glimmr.Views.dll
pscp -P 22 -r -pw intozoom .\Glimmr.deps.json intozoom@%1:/Library/Glimmr/1.2.0/Glimmr.deps.json
pscp -P 22 -r -pw intozoom .\Glimmr.Views.pdb intozoom@%1:/Library/Glimmr/1.2.0/Glimmr.Views.pdb
pscp -P 22 -r -pw intozoom .\wwwroot\js\* intozoom@%1:/Library/Glimmr/1.2.0/wwwroot/js/
pscp -P 22 -r -pw intozoom .\wwwroot\css\* intozoom@%1:/Library/Glimmr/1.2.0/wwwroot/css/
pscp -P 22 -r -pw intozoom .\Glimmr.dll intozoom@%1:/Library/Glimmr/1.2.0/Glimmr.dll
pscp -P 22 -r -pw intozoom .\Glimmr.pdb intozoom@%1:/Library/Glimmr/1.2.0/Glimmr.pdb
pscp -P 22 -r -pw intozoom .\Glimmr.xml intozoom@%1:/Library/Glimmr/1.2.0/Glimmr.xml
pscp -P 22 -r -pw intozoom .\Glimmr.runtimeconfig.json intozoom@%1:/Library/Glimmr/1.2.0/Glimmr.runtimeconfig.json
pscp -P 22 -r -pw intozoom .\Glimmr intozoom@%1:/Library/Glimmr/1.2.0/Glimmr
GOTO NEXT
:JS
echo Copying JS files...
pscp -P 22 -r -pw intozoom .\wwwroot\js\* intozoom@%1:/Library/Glimmr/1.2.0/wwwroot/js/
GOTO END
:CSS
echo Copying CSS files...
pscp -P 22 -r -pw intozoom .\wwwroot\css\* intozoom@%1:/Library/Glimmr/1.2.0/wwwroot/css/
GOTO END
:WEB
echo Copying Web files...
pscp -P 22 -r -pw intozoom .\wwwroot\* intozoom@%1:/Library/Glimmr/1.2.0/wwwroot/
GOTO END
:FULL
echo Copying All files...
pscp -P 22 -r -pw intozoom .\* intozoom@%1:/Library/Glimmr/1.2.0/
:NEXT
IF NOT "%2"=="-s" GOTO END


GOTO END
:HELP
echo _______________________________
echo Script Usage:
echo publish_osx.bat <IP_OF_GLIMMR> -c/-f/-k/-j/-s
echo OR
echo publish_osx.bat -h (Show Help)
echo _______________________________
echo Flags:
echo -c: Copy CSS only, don't restart
echo -f: Full copy, use AFTER -s or -k flags
echo -k: Kill Glimmr, assumes is being run manually. Can be used before the -f flag;
echo -j: Copy Java only, don't restart
echo -s: Stop glimmr service, then copy. Can be used before the -f flag.
echo _______________________________
:END
cd ../../../..
