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


Stop-Process -name "Glimmr" -ErrorAction SilentlyContinue;

Write-Host Extracting release files
Expand-Archive -Path $zip -DestinationPath $appdir

# Delete extracted archive
Remove-Item $zip -Force -ErrorAction SilentlyContinue