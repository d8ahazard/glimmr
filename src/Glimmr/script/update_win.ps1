# Download latest dotnet/codeformatter release from github
$releases = "https://api.github.com/repos/d8ahazard/glimmr/releases"
$appdir = "$Env:ProgramFiles\Glimmr\"
Write-Host "Determining latest Glimmr release."
$tag = (Invoke-WebRequest $releases | ConvertFrom-Json)[0].tag_name
Write-Host "Latest Glimmr version is $tag."

# Self-elevate the script if required
if (-Not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]'Administrator'))
{
    if ([int](Get-CimInstance -Class Win32_OperatingSystem | Select-Object -ExpandProperty BuildNumber) -ge 6000)
    {
        $CommandLine = "-File `"" + $MyInvocation.MyCommand.Path + "`" " + $MyInvocation.UnboundArguments
        Start-Process -FilePath PowerShell.exe -Verb Runas -ArgumentList $CommandLine
        Exit
    }
}

$download = "https://github.com/d8ahazard/glimmr/releases/download/$tag/Glimmr.$tag.win-x64.zip"
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