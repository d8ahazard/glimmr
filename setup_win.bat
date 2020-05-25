@echo off
echo "Downloading git exe"
set installDir="C:\Program Files\Git"
set installDir=%installDir:"=%
git
if errorlevel 0 goto installGlimmr

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
    @echo on
    @echo Error finding "git*.exe" install executable. File may not exist or is not named with the "git" prefix.
    exit /b 2
)

@echo on
@echo Installing..
@echo off
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
powershell %~dp0dotnet-install.ps1 -Channel 3.1

:installGlimmr
echo installing to "%~dp0glimmr.exe"
git clone https://github.com/d8ahazard/glimmr ./src/
cd ./src/
copy ./build/win/bass.dll ../x64/bass.dll
./publishWIN.bat
nssm install glimmr "%~dp0glimmr.exe"
nssm set glimmr AppDirectory %~dp0
nssm set glimmr DisplayName GlimmrTV
nssm set glimmr Description Glimmr TV Ambient Lighting Service
nssm set glimmr Start SERVICE_AUTO_START