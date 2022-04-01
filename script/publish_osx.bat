@echo off
if "%1"=="-h" GOTO HELP
IF NOT "%2"=="-s" GOTO BUILD
echo Stopping Glimmr
plink -no-antispoof -pw Digitalhigh digitalhigh@%1 "echo digitalhigh | sudo -S pkill -f Glimmr"

:BUILD
IF NOT "%2"=="-k" GOTO BUILD2
echo Killing Glimmr task...
plink -no-antispoof -pw Digitalhigh digitalhigh@%1 "echo digitalhigh | sudo -S pkill -f Glimmr"

:BUILD2
cd ..\src\

echo OSX...
for %%x in (
	osx-x64
) do (
	echo Building %%x
	dotnet publish -r %%x -c Release .\Glimmr\Glimmr.csproj -o .\Glimmr\bin\%%x --self-contained=true
    dotnet publish -r %%x -c release .\Image2Scene\Image2Scene.csproj -o .\Glimmr\bin\%%x --self-contained=true
    echo Archiving %%x
    %~dp07z.exe a -ttar -so -an -r .\Glimmr\bin\%%x\* | %~dp07z a -si .\Glimmr\bin\Glimmr-%%x-%version%.tgz
    echo Copying OSX Files...
    del /S /Q ..\Glimmr-macos-installer-builder\macOS-x64\application\*
    xcopy /Y /E .\Glimmr\bin\osx-x64\* ..\Glimmr-macos-installer-builder\macOS-x64\application
)

if "%1"=="" GOTO :END
cd .\Glimmr\bin\osx-x64
IF "%2"=="-j" GOTO JS
IF "%2"=="-c" GOTO CSS
IF "%2"=="-w" GOTO WEB

IF "%2"=="-f" GOTO FULL
echo Copying main files...
pscp -P 22 -r -pw digitalhigh .\Glimmr.Views.dll Digitalhigh@%1:/Library/Glimmr/1.2.0/Glimmr.Views.dll
pscp -P 22 -r -pw digitalhigh .\Glimmr.deps.json Digitalhigh@%1:/Library/Glimmr/1.2.0/Glimmr.deps.json
pscp -P 22 -r -pw digitalhigh .\Glimmr.Views.pdb Digitalhigh@%1:/Library/Glimmr/1.2.0/Glimmr.Views.pdb
pscp -P 22 -r -pw digitalhigh .\wwwroot\js\* Digitalhigh@%1:/Library/Glimmr/1.2.0/wwwroot/js/
pscp -P 22 -r -pw digitalhigh .\wwwroot\css\* Digitalhigh@%1:/Library/Glimmr/1.2.0/wwwroot/css/
pscp -P 22 -r -pw digitalhigh .\Glimmr.dll Digitalhigh@%1:/Library/Glimmr/1.2.0/Glimmr.dll
pscp -P 22 -r -pw digitalhigh .\Glimmr.pdb Digitalhigh@%1:/Library/Glimmr/1.2.0/Glimmr.pdb
pscp -P 22 -r -pw digitalhigh .\Glimmr.xml Digitalhigh@%1:/Library/Glimmr/1.2.0/Glimmr.xml
pscp -P 22 -r -pw digitalhigh .\Glimmr.runtimeconfig.json Digitalhigh@%1:/Library/Glimmr/1.2.0/Glimmr.runtimeconfig.json
pscp -P 22 -r -pw digitalhigh .\Glimmr Digitalhigh@%1:/Library/Glimmr/1.2.0/Glimmr
GOTO NEXT
:JS
echo Copying JS files...
pscp -P 22 -r -pw digitalhigh .\wwwroot\js\* Digitalhigh@%1:/Library/Glimmr/1.2.0/wwwroot/js/
GOTO END
:CSS
echo Copying CSS files...
pscp -P 22 -r -pw digitalhigh .\wwwroot\css\* Digitalhigh@%1:/Library/Glimmr/1.2.0/wwwroot/css/
GOTO END
:WEB
echo Copying Web files...
pscp -P 22 -r -pw digitalhigh .\wwwroot\* Digitalhigh@%1:/Library/Glimmr/1.2.0/wwwroot/
GOTO END
:FULL
echo Copying All files...
pscp -P 22 -r -pw digitalhigh .\* Digitalhigh@%1:/Library/Glimmr/1.2.0/
:NEXT
IF NOT "%2"=="-s" GOTO END


GOTO END
:HELP
echo _______________________________
echo Script Usage:
echo Edit the credentials in the top of the script to work for your OSX machine.
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
cd ..\..\..\..
