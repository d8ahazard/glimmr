@echo off

cd ..\src\Glimmr

set fullVersion=""
set VERSION=""

del /Q /S .\bin\*.tgz
del /Q /S .\bin\*.zip
del /Q /S .\bin\*.rpm
del /Q /S .\bin\*.deb
del /Q /S .\bin\*.exe

echo Linux...
for %%x in (
    linux-x64        
    linux-arm64
    linux-arm
) do (
	echo Building %%x
	dotnet restore -r %%x
	echo Creating DEB/RPM for %%x
    dotnet deb -c Release -o .\bin -r %%x
    dotnet rpm -c Release -o .\bin -r %%x
    echo Archiving %%x
    dotnet tarball -c Release -o .\bin -r %%x
        
    if "%%x" == "linux-arm64" (
        for /f "delims=" %%a in ('DIR /b /a-d .\bin\Glimmr.*.linux-x64.deb') do set name=%%a
        echo %name:~0,-14%
        set fullVersion=%name:~0,-14%
        echo FULL VERSION: %fullVersion%
        for /F "tokens=1,2,3 delims='-'" %%a in ("%fullVersion:~7%") do (
           set VERSION=%%a
        )
        echo VERSION: %VERSION%
        echo Copying x64 RPI files for %%x
        xcopy /Y .\bin\%fullVersion%.%%x.tar.gz ..\..\Glimmr-image-gen-x64\stage2\05-glimmr\files\archive.tgz
        pause
    )
    if "%%x" == "linux-arm" (
        echo Copying x86 RPI files for %%x
        xcopy /Y .\bin\%fullVersion%.%%x.tar.gz ..\..\Glimmr-image-gen\stage2\05-glimmr\files\archive.tgz
    )
)

for /f "delims=" %%a in ('DIR /b /a-d .\bin\Glimmr.*.linux-x64.deb') do set "name=%%a"
echo %name:~0,-14%
set fullVersion=%name:~0,-14%
echo FULL VERSION: %fullVersion%
for /F "tokens=1,2,3 delims='-'" %%a in ("%fullVersion:~7%") do (
   set VERSION=%%a
)
echo VERSION: %VERSION%

echo Windows...
for %%x in (	
    win-arm64
    win-x64
    win-x86
) do (
	echo Building %%x
	dotnet restore -r %%x
	dotnet zip -c Release -o .\bin -r %%x
    echo Building MSI for %%x
    "C:\Progra~2\Inno Setup 6\iscc.exe" /F%fullVersion%.%%x %~dp0..\src\Glimmr\build_%%x.iss
)


echo OSX...
for %%x in (
	osx-x64
) do (
	echo Building %%x
	dotnet restore -r %%x
	dotnet tarball -c Release -o .\bin -r %%x
    echo Copying OSX Files...
    del /S /Q ..\..\Glimmr-macos-installer-builder\macOS-x64\application\*
    xcopy /Y /E .\bin\osx-x64\* ..\..\Glimmr-macos-installer-builder\macOS-x64\application
)

cd ../../script