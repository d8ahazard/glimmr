@echo off
set installDir="C:\Program Files\Git"
set installDir=%installDir:"=%
git
if errorlevel 0 goto installGlimmr
echo "Downloading git exe"
set url=https://github.com/git-for-windows/git/releases/download/v2.26.2.windows.1/Git-2.26.2-64-bit.exe
set file=git_install.exe
certutil -urlcache -split -f %url% %file%
set url=https://dot.net/v1/dotnet-install.ps1
set file=dotnet-install.ps1
certutil -urlcache -split -f %url% %file%
(
    echo [Setup]
    echo Lang=default
    echo Dir=%installDir%
    echo Group=Git
    echo NoIcons=0
    echo SetupType=default
    echo Components=icons,ext\reg\shellhere,assoc,assoc_sh
    echo Tasks=
    echo PathOption=Cmd
    echo SSHOption=OpenSSH
    echo CRLFOption=CRLFAlways
    echo BashTerminalOption=ConHost
    echo PerformanceTweaksFSCache=Enabled
    echo UseCredentialManager=Enabled
    echo EnableSymlinks=Disabled
    echo EnableBuiltinDifftool=Disabled
) > config.inf

set file="%~dp0git.exe"

if [%file%]==[] (
    echo Error finding "git*.exe" install executable. File may not exist or is not named with the "git" prefix.
    exit /b 2
)

echo Installing git.
%file% /VERYSILENT /LOADINF="config.inf"
if errorlevel 1 (
    @echo on
    if %errorLevel% == 1 ( echo Error opening %file%. File may be corrupt. )
    if %errorLevel% == 2 ( echo Error reading %file%. May require elevated privileges. Run as administrator. )
    exit /b %errorlevel%
)
del config.inf

net session >nul 2>&1
if %errorLevel% == 0 (
    pathman /as "%PATH%;%installDir%/cmd"
    exit 0
) else (
    @echo on
    echo SYSTEM PATH Environment Variable may not be set, may require elevated privileges. Run as administrator if it doesn't already exist.
    exit /b 0
)

dotnet
if errorlevel 0 goto installGlimmr
echo Installing dotnet binaries.
powershell %~dp0dotnet-install.ps1 -Channel 5.0

:installGlimmr
set file="%~dp0src"

if not exist %file% (
    echo Cloning repository    
    git clone https://github.com/d8ahazard/glimmr %~dp0src/
) else (
	cd %~dp0src/
	echo Dir already exists %file%
    git stash && git fetch && git pull
)

cd %~dp0src/
net stop GlimmrTV
echo Publishing for windows
set version=1.1.0
dotnet build Glimmr.csproj /p:DeployOnBuild=true /p:PublishProfile=Windows
echo copying bass.dll from %~dp0src\build\win\bass.dll to %~dp0x64\bass.dll
copy %~dp0src\build\win\bass.dll %~dp0x64\bass.dll
mkdir %~dp0wwwroot
xcopy %~dp0src\bin\publish\Glimmr-windows\* %~dp0\ /s /i /y
xcopy %~dp0src\wwwroot\* %~dp0wwwroot\ /s /i /y
net start GlimmrTV
pause
if errorlevel 0 goto installService
goto noInstall
:installService
echo Installing glimmr service...
nssm install glimmr "%~dp0glimmr.exe"
nssm set glimmr AppDirectory %~dp0
nssm set glimmr DisplayName GlimmrTV
nssm set glimmr Description Glimmr TV Ambient Lighting Service
nssm set glimmr Start SERVICE_AUTO_START
goto EOF
:noInstall
echo Service installation skipped.