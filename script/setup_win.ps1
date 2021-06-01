
$software = "Git Version 2.31.1";
$installed = (Get-ItemProperty HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\* | Where { $_.DisplayName -eq $software }) -ne $null
$gitPath = (get-command git.exe -ErrorAction SilentlyContinue).Path;
if ($gitPath -ne $null) {
    $installed = $true;
} else {
    $gitPath1 = "C:\Progra~1\Git\bin\git.exe";
    $gitPath2 = "C:\Progra~2\Git\bin\git.exe";
    if (Test-Path -Path $gitPath1) {
        $gitPath = $gitPath1;
        $installed = $true;
    }

    if (Test-Path -Path $gitPath2) {
        $gitPath = $gitPath2;
        $installed = $true;
    }
}


If(-Not $installed) {
	Write-Host "'$software' NOT is installed.";
    $gitPath = "C:\Progra~2\Git\bin\git.exe"
    $URL = "https://github.com/git-for-windows/git/releases/download/v2.31.1.windows.1/Git-2.31.1-32-bit.exe"
    $outfile = "C:\temp\gitInstall.exe"
    Invoke-WebRequest -Uri $URL -OutFile $outfile
     
    If( -not (Test-Path -Path $outfile) ){
        Throw "Download failed"
    }

    Write-Host "Installing git, please wait."
    Start-Process -FilePath $outfile -ArgumentList "/VERYSILENT","/NORESTART" -Wait    
    Remove-Item -Path $outfile -Force
} else {
	Write-Host "Git found at $gitPath";
}

If( -not (Test-Path -Path $gitPath) ){
    Throw "git.exe not found."
}

$software = "Microsoft .NET Runtime - 5.0.5 (x64)";
$installed = (Get-ItemProperty HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\* | Where { $_.DisplayName -eq $software }) -ne $null
$dotNetPath = (get-command dotnet.exe -ErrorAction SilentlyContinue).Path;

if ($dotNetPath -ne $null) {
    $installed = $true;
} else {
    $dotNetPath = "C:\progra~1\dotnet\dotnet.exe";
    if (Test-Path -Path $dotNetPath) {
        $installed = $true;
    }
}


If(-Not $installed) {
	Write-Host "'$software' is NOT installed.";
    $URL = "https://download.visualstudio.microsoft.com/download/pr/2de622da-5342-48ec-b997-8b025d8ee478/5c11b643ea7534f749cd3f0e0302715a/dotnet-sdk-5.0.202-win-x64.exe"
    $outfile = "C:\temp\dotNetInstall.exe"
    Invoke-WebRequest -Uri $URL -OutFile $outfile
     
    If( -not (Test-Path -Path $outfile) ){
        Throw "Download failed"
    }

    Start-Process -FilePath $outfile -ArgumentList "/install","/quiet","/norestart" -Wait
    Remove-Item -Path $outfile -Force
    Write-Host "'$software' should now be installed.";
    $dotNetPath = (get-command dotnet.exe -ErrorAction SilentlyContinue).Path;
} else {
	Write-Host "'$software' is installed at $dotNetPath."
}

If( -not (Test-Path -Path $dotNetPath) ){
    Throw "dotnet.exe not found."
} else {
    Write-Host "Dotnet found at $dotNetPath";
}

$software = "Microsoft .NET Framework Framework 4.7.2 SDK";

Write-Host "'$software' is NOT installed.";
$URL="https://dotnet.microsoft.com/download/dotnet-framework/thank-you/net472-developer-pack-offline-installer";
$outfile = "C:\temp\dotNetDevPack.exe"
Invoke-WebRequest -Uri $URL -OutFile $outfile
 
If( -not (Test-Path -Path $outfile) ){
    Throw "Download failed"
}

Start-Process -FilePath $outfile -ArgumentList "/passive","/norestart" -Wait
Remove-Item -Path $outfile -Force
Write-Host "'$software' should now be installed.";


# Self-elevate the script if required
if (-Not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] 'Administrator')) {
 if ([int](Get-CimInstance -Class Win32_OperatingSystem | Select-Object -ExpandProperty BuildNumber) -ge 6000) {
  $CommandLine = "-File `"" + $MyInvocation.MyCommand.Path + "`" " + $MyInvocation.UnboundArguments
  Start-Process -FilePath PowerShell.exe -Verb Runas -ArgumentList $CommandLine
  Exit
 }
}

$service = Get-Service -Name glimmr -ErrorAction SilentlyContinue


$glimmrPath = "C:\Progra~1\Glimmr";
$glimmrBinPath = "C:\Progra~1\Glimmr\bin\Glimmr.exe";
$glimmrRepo = "https://github.com/d8ahazard/glimmr";

If( -not (Test-Path -Path $glimmrPath) ){
    Write-Host "Cloning Glimmr repository.";
    Invoke-Expression "& '$gitPath' clone --branch dev $glimmrRepo $glimmrPath";
    
    if($service -ne $null) {
        Stop-Service -Name "glimmr";
        Write-Host "Removing glimmr service.";
        $nssm = 'C:\progra~1\Glimmr\script\nssm.exe';
        $param = 'remove glimmr';
        Invoke-Expression "$nssm $param";    
    }
    
    Stop-Process -name "Glimmr" -ErrorAction SilentlyContinue;

} else {
    if($service -ne $null) {
        Stop-Service -Name "glimmr";
        Write-Host "Removing glimmr service.";
        $nssm = 'C:\progra~1\Glimmr\script\nssm.exe';
        $param = 'remove glimmr';
        Invoke-Expression "$nssm $param";    
    }
    
    Stop-Process -name "Glimmr" -ErrorAction SilentlyContinue;

    Set-Location -path "C:\program files\glimmr"; 
    Write-Host "Glimmr repo already exists...";
    Invoke-Expression "& '$gitPath' stash";
    Invoke-Expression "& '$gitPath' fetch";
    Invoke-Expression "& '$gitPath' pull";
}

If( -not (Test-Path -Path $glimmrPath) ){
    Write-Host "Glimmr path doesn't exist...something went wrong.";
} else {
    Write-Host "Compiling Glimmr...";
    $projectPath = "$glimmrPath\src\Glimmr.csproj";
    $outPath = "$glimmrPath\bin";    
    Invoke-Expression "& '$dotNetPath' publish $projectPath /p:PublishProfile=Windows -o $outPath";
    Copy-Item "$glimmrPath\lib\Windows\bass.dll" "$outPath\bass.dll";
    Copy-Item "$glimmrPath\lib\Windows\GlimmrTray.exe" "$outPath\GlimmrTray.exe";
}
$dirPath = $env:USERPROFILE + "\AppData\Roaming\Microsoft\Windows\Start Menu\Programs";
$dirPath2 = $dirPath + "\Glimmr\";
Write-Host "Creating $dirPath"; 

$objShell = New-Object -ComObject ("WScript.Shell");
$objShortCut = $objShell.CreateShortcut($dirPath + "\Startup" + "\GlimmrTray.lnk");
$objShortCut.TargetPath=$glimmrBinPath;
$objShortCut.Save();
New-Item -Path $dirPath -Name "Glimmr" -ItemType "directory" -ErrorAction SilentlyContinue
New-Item -Path $dirPath2 -Name "Glimmr" -ItemType "directory" -ErrorAction SilentlyContinue
$objShortCut2 = $objShell.CreateShortcut($dirPath2 + "\GlimmrTray.lnk");
$objShortCut2.TargetPath=$glimmrBinPath;
$objShortCut2.Save();

Write-Host "Glimmr has been installed, launching tray!";
Start-Process -FilePath "C:\program files\Glimmr\bin\GlimmrTray.exe";
pause