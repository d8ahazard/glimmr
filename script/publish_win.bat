@echo off
dotnet publish -c release ..\src\Glimmr.csproj /p:PublishProfile=Windows -o ..\src\bin\Windows
xcopy /y ..\lib\win\* ..\src\bin\Windows\
if not exist "..\src\bin\ambientScenes" mkdir ..\src\bin\Windows\ambientScenes
if not exist "..\src\bin\audioScenes" mkdir ..\src\bin\Windows\audioScenes
xcopy /y ..\ambientScenes\* ..\src\bin\Windows\ambientScenes\
xcopy /y ..\audioScenes\* ..\src\bin\Windows\audioScenes\
