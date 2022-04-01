@echo off
if "%~1"=="-r" taskkill /IM Glimmr.exe
cd ..\src\
for %%x in (
    win-x64
) do (
	echo Building %%x
	dotnet publish -r %%x -c Release .\Glimmr\Glimmr.csproj -o .\Glimmr\bin\%%x --self-contained=true
	dotnet publish -r %%x -c release .\GlimmrTray\GlimmrTray.csproj -o .\Glimmr\bin\%%x --self-contained=true
    dotnet publish -r %%x -c release .\Image2Scene\Image2Scene.csproj -o .\Glimmr\bin\%%x --self-contained=true
)
if "%~1"=="-r" GOTO LAUNCH
GOTO END
:LAUNCH
start /D .\Glimmr\bin\win-x64\Glimmr.exe
cd ..\..\..\..
:END