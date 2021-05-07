@echo off

dotnet --info >nul 2>&1
if errorlevel 0 goto CheckAdmin
REM We need to run this *WITHOUT* admin
echo Installing dotnet binaries, path is %~dp0.
set url=https://dot.net/v1/dotnet-install.ps1
set file=%~dp0dotnet-install.ps1
powershell Invoke-WebRequest -Uri %url% -OutFile %file%

powershell %~dp0dotnet-install.ps1 -Channel 5.0
pause

:CheckAdmin
:: BatchGotAdmin
:-------------------------------------
REM  --> Check for permissions
>nul 2>&1 "%SYSTEMROOT%\system32\cacls.exe" "%SYSTEMROOT%\system32\config\system"

REM --> If error flag set, we do not have admin.
if '%errorlevel%' NEQ '0' (
    echo Please re-run this script as an administrator...
    pause
	goto EOF
)
:gotAdmin
echo We have admin permissions, checking git installation

set installDir=C:\Progra~1\Git
set glimmrDir=C:\Progra~1\Glimmr
set installDir=%installDir:"=%
git >nul 2>&1
if errorlevel 0 goto installGlimmr
echo "Downloading git exe"
set url=https://github.com/git-for-windows/git/releases/download/v2.31.1.windows.1/Git-2.31.1-64-bit.exe
set file=%~dp0Git-2.31.1-64-bit.exe
powershell Invoke-WebRequest -Uri %url% -OutFile %file%

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


:installGlimmr
echo "Pre-requisites are good, installing Glimmr."
pause
set file="%glimmrDir%\bin\Glimmr.exe"
echo checking for %file%
if not exist %file% (
	rd /s /q "%glimmrDir%"
    echo Cloning repository    
    git clone --branch dev https://github.com/d8ahazard/glimmr "%glimmrDir%"
) else (
	cd %glimmrDir%
	echo Dir already exists %file%
    git stash && git fetch && git pull
)

cd "%glimmrDir%"
net stop GlimmrTV
echo Publishing for windows
set version=1.1.0
%localappdata%\Microsoft\dotnet\dotnet publish "%glimmrDir%\src\Glimmr.csproj" /p:PublishProfile=Windows -o "%glimmrDir%\bin\"
echo copy %glimmrDir%\lib\Windows\bass.dll %glimmrDir%\bin\bass.dll
copy %glimmrDir%\lib\Windows\bass.dll %glimmrDir%\bin\bass.dll
net start GlimmrTV
pause
if errorlevel 0 goto installService
goto noInstall
:installService
echo Installing glimmr service...
"%glimmrDir%\script\nssm.exe" install glimmr "%glimmrDir%\bin\Glimmr.exe"
"%glimmrDir%\script\nssm.exe" set glimmr AppDirectory %glimmrDir%\bin
"%glimmrDir%\script\nssm.exe" set glimmr DisplayName GlimmrTV
"%glimmrDir%\script\nssm.exe" set glimmr Description Glimmr TV Ambient Lighting Service
"%glimmrDir%\script\nssm.exe" set glimmr Start SERVICE_AUTO_START
net start glimmr
goto EOF
:noInstall
echo Service installation skipped.
:EOF