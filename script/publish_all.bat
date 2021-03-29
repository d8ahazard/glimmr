@echo off
IF [%1] == [] GOTO Error
set version=%~1


@echo off

for %%x in (
	Linux
	LinuxARM
	Windows
	WindowsARM
	OSX
) do (
	echo Building %%x
	dotnet publish -c Release ..\src\Glimmr.csproj /p:PublishProfile=%%x -o ..\src\bin\%%x
	if exist "..\lib\%%x" xcopy /y ..\lib\%%x\* ..\src\bin\%%x\
	if not exist "..\src\bin\ambientScenes" mkdir ..\src\bin\%%x\ambientScenes
	if not exist "..\src\bin\audioScenes" mkdir ..\src\bin\%%x\audioScenes
	xcopy /y ..\ambientScenes\* ..\src\bin\%%x\ambientScenes\
	xcopy /y ..\audioScenes\* ..\src\bin\%%x\audioScenes\	
)

:Archive
cd ..\src\bin\
%~dp07z.exe a -ttar -so -an -r .\LinuxARM\* | %~dp07z a -si Glimmr-linux-arm-%version%.tgz
%~dp07z.exe a -ttar -so -an -r .\Linux\* | %~dp07z a -si Glimmr-linux-%version%.tgz
%~dp07z.exe a -tzip -r Glimmr-windows-%version%.zip .\Windows\*
%~dp07z.exe a -tzip -r Glimmr-windows-arm-%version%.zip .\WindowsARM\*
%~dp07z.exe a -tzip -r Glimmr-osx-%version%.zip .\OSX\*
GOTO End

:Error
echo Please enter a version number.

:End