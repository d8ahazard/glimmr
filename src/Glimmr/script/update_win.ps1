# Download latest dotnet/codeformatter release from github
$releases = "https://api.github.com/repos/d8ahazard/glimmr/releases"
$appdir = "$Env:ProgramFiles\Glimmr\"
Write-Host "Determining latest Glimmr release."
$tag = (Invoke-WebRequest $releases | ConvertFrom-Json)[0].tag_name
Write-Host "Latest Glimmr version is $tag."
# Check for existing "version" file, refer to that to determine if update is needed
$file = "$Env:ProgramData\Glimmr\"
if (Test-Path -Path $file -PathType Leaf) {
    $existing = Get-Content -Path $file
    Write-Host "Existing Glimmr version is $existing."
    if([System.Version]$tag -le [System.Version]$existing) {
        Write-Host "Nothing to do."
        Exit
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

Stop-Process -name "Glimmr.exe" -ErrorAction SilentlyContinue;
Stop-Process -name "GlimmrTray.exe" -ErrorAction SilentlyContinue;

Write-Host Extracting release files
Expand-Archive -Path $zip -DestinationPath $appdir

# Delete extracted archive
Remove-Item $zip -Force -ErrorAction SilentlyContinue

Start-Process -FilePath "$Env:ProgramFiles\Glimmr\GlimmrTray.exe"