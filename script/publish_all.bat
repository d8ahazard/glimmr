@echo off
set version=""
set fullVersion=""
cd ..\src\

del /Q /S .\Glimmr\bin\*.tgz
del /Q /S .\Glimmr\bin\*.zip
del /Q /S .\Glimmr\bin\*.rpm
del /Q /S .\Glimmr\bin\*.deb
del /Q /S .\Glimmr\bin\*.exe

echo Creating DEB/RPM for linux-x64
cd .\Glimmr
dotnet deb -c Release -o .\bin -r linux-x64
for /f "delims=" %%a in ('DIR /b /a-d .\bin\Glimmr.*.linux-x64.deb') do set "name=%%a"
echo %name:~0,-14%
set fullVersion=%name:~0,-14%
echo FULL VERSION: %fullVersion%
for /F "tokens=1,2,3 delims='-'" %%a in ("%fullVersion:~7%") do (
   set VERSION=%%a
)
echo VERSION: %VERSION%
cd ..

echo Windows...
for %%x in (	
    win-arm64
    win-x64
    win-x86
) do (
	echo Building %%x
	dotnet publish -r %%x -c Release .\Glimmr\Glimmr.csproj -o .\Glimmr\bin\%%x --self-contained=true
    dotnet publish -r %%x -c release .\Image2Scene\Image2Scene.csproj -o .\Glimmr\bin\%%x --self-contained=true
    dotnet publish -r %%x -c release .\GlimmrTray\GlimmrTray.csproj -o .\Glimmr\bin\%%x --self-contained=true
    echo Archiving %%x
    %~dp07z.exe a -tzip -r .\Glimmr\bin\%fullVersion%.%%x.zip .\Glimmr\bin\%%x\*
    echo Building MSI for %%x
    "C:\Progra~2\Inno Setup 6\iscc.exe" /F%fullVersion%.%%x %~dp0..\src\Glimmr\build_%%x.iss
)

echo Linux...
for %%x in (
    linux-x64
    linux-arm
    linux-arm64
) do (
	echo Building %%x
	dotnet publish -r %%x -c Release .\Glimmr\Glimmr.csproj -o .\Glimmr\bin\%%x --self-contained=true
    dotnet publish -r %%x -c release .\Image2Scene\Image2Scene.csproj -o .\Glimmr\bin\%%x --self-contained=true
    cd .\Glimmr
    echo Creating DEB/RPM for %%x
    dotnet deb -c Release -o .\bin -r %%x
    dotnet rpm -c Release -o .\bin -r %%x
    pause
    cd ..
        
    echo Archiving %%x
    %~dp07z.exe a -ttar -so -an -r .\Glimmr\bin\%%x\* | %~dp07z a -si .\Glimmr\bin\%fullVersion%.%%x.tgz
    if ["%%x"] == "linux-arm64" (
        echo Copying x64 RPI files for %%x
        xcopy /-I /Y .\Glimmr\bin\%fullVersion%.%%x.tgz ..\Glimmr-image-gen-x64\stage2\05-glimmr\files\archive.tgz
    )
    if ["%%x"] == "linux-arm" (
        echo Copying x86 RPI files for %%x
        xcopy /-I /Y .\Glimmr\bin\%fullVersion%.%%x.tgz ..\Glimmr-image-gen\stage2\05-glimmr\files\archive.tgz
    )
)


echo OSX...
for %%x in (
	osx-x64
) do (
	echo Building %%x
	dotnet publish -r %%x -c Release .\Glimmr\Glimmr.csproj -o .\Glimmr\bin\%%x --self-contained=true
    dotnet publish -r %%x -c release .\Image2Scene\Image2Scene.csproj -o .\Glimmr\bin\%%x --self-contained=true
    echo Archiving %%x
    %~dp07z.exe a -ttar -so -an -r .\Glimmr\bin\%%x\* | %~dp07z a -si .\Glimmr\bin\%fullVersion%.%%x.tgz
    echo Copying OSX Files...
    del /S /Q ..\Glimmr-macos-installer-builder\macOS-x64\application\*
    xcopy /Y /E .\Glimmr\bin\osx-x64\* ..\Glimmr-macos-installer-builder\macOS-x64\application
)

cd ../script