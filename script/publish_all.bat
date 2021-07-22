@echo off
IF [%1] == [] GOTO Error
set version=%~1

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