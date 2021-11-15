# Download latest dotnet/codeformatter release from github
$releases = "https://api.github.com/repos/d8ahazard/glimmr/releases"
$appdir = "$Env:Programfiles\Glimmr\"
Write-Host "Determining latest release."
$tag = (Invoke-WebRequest $releases | ConvertFrom-Json)[0].tag_name
Write-Host "Latest release version is $tag."
# Check for existing "version" file, refer to that to determine if update is needed
$file = "$Env:ProgramData\Glimmr\"
if (Test-Path -Path $file -PathType Leaf) {
    $existing = Get-Content -Path $file
    Write-Host "Existing version is $existing."
    if([System.Version]$tag -le [System.Version]$existing) {
        Write-Host "Version is older or equal, nothing to do."
    }
}

# Self-elevate the script if required
if (-Not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] 'Administrator')) {
    if ([int](Get-CimInstance -Class Win32_OperatingSystem | Select-Object -ExpandProperty BuildNumber) -ge 6000) {
        $CommandLine = "-File `"" + $MyInvocation.MyCommand.Path + "`" " + $MyInvocation.UnboundArguments
        Start-Process -FilePath PowerShell.exe -Verb Runas -ArgumentList $CommandLine
        Exit
    }
}

$download = "https://github.com/d8ahazard/glimmr/releases/download/$tag/Glimmr-windows-$tag.zip"
$zip = "$Env:ProgramData\Glimmr\archive.zip"

Write-Host "Dowloading latest release"
Invoke-WebRequest $download -Out $zip

$service = Get-Service -Name glimmr -ErrorAction SilentlyContinue

Write-Host "Stopping and removing service definitions."

$glimmrPath = "$Env:Programfiles\Glimmr";
$glimmrBinPath = "$Env:Programfiles\Glimmr\Glimmr.exe";
$glimmrRepo = "https://github.com/d8ahazard/glimmr";

if($service -ne $null) {
    Stop-Service -Name "glimmr";
    Write-Host "Removing glimmr service.";
    $nssm = '$Env:Programfiles\Glimmr\nssm.exe';
    $param = 'remove glimmr';
    Invoke-Expression "$nssm $param";
}

Stop-Process -name "Glimmr" -ErrorAction SilentlyContinue;

Write-Host Extracting release files
Expand-Archive -Path $zip -DestinationPath $appdir

# Delete extracted archive
Remove-Item $zip -Force -ErrorAction SilentlyContinue

$dirPath = $env:USERPROFILE + "\AppData\Roaming\Microsoft\Windows\Start Menu\Programs";
$dirPath2 = $dirPath + "\Glimmr\";

New-Item -Path $dirPath -Name "Glimmr" -ItemType "directory" -ErrorAction SilentlyContinue
New-Item -Path $dirPath2 -Name "Glimmr" -ItemType "directory" -ErrorAction SilentlyContinue

$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$dirPath2\Glimmr.lnk")
$Shortcut.TargetPath = "$Env:Programfiles\Glimmr\Glimmr.exe"
$Shortcut.Save()

$bytes = [System.IO.File]::ReadAllBytes("$dirPath2\Glimmr.lnk")
$bytes[0x15] = $bytes[0x15] -bor 0x20 #set byte 21 (0x15) bit 6 (0x20) ON
[System.IO.File]::WriteAllBytes("$dirPath2\Glimmr.lnk", $bytes)
Copy-Item "$$dirPath2\Glimmr.lnk" "$dirPath" + "\Startup";

$Shortcut2 = $WshShell.CreateShortcut("$dirPath2\GlimmrTray.lnk")
$Shortcut2.TargetPath = "$Env:Programfiles\Glimmr\GlimmrTray.exe"
$Shortcut2.Save()

$bytes2 = [System.IO.File]::ReadAllBytes("$dirPath2\GlimmrTray.lnk")
$bytes2[0x15] = $bytes2[0x15] -bor 0x20 #set byte 21 (0x15) bit 6 (0x20) ON
[System.IO.File]::WriteAllBytes("$dirPath2\GlimmrTray.lnk", $bytes2)
Copy-Item "$$dirPath2\GlimmrTray.lnk" "$dirPath" + "\Startup";

Write-Host "Glimmr has been installed, launching tray!";
Start-Process -FilePath "C:\program files\Glimmr\GlimmrTray.exe";
pause