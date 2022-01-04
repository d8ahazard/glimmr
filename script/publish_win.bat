@echo off
if "%~1"=="-r" taskkill /IM Glimmr.exe
dotnet publish -c release ..\src\Glimmr\Glimmr.csproj /p:PublishProfile=Windows -o ..\src\Glimmr\bin\Windows
dotnet publish -r win-x64 -c release ..\src\GlimmrTray\GlimmrTray.csproj -o ..\src\Glimmr\bin\Windows
if "%~1"=="-r" GOTO LAUNCH
GOTO END
:LAUNCH
start /D ..\src\Glimmr\bin\Windows\ ..\src\Glimmr\bin\Windows\Glimmr.exe
:END