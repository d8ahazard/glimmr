$targets = "linux-arm64", "linux-arm", "linux-x64", "win-arm64", "win-x64", "win-x86", "osx-x64"
$extensions = "tgz", "tar.gz", "zip", "rpm", "deb", "exe", "msi"
$version = ""
$fullVersion = ""
$innoPath = "C:\Progra~2\Inno Setup 6\iscc.exe"
$glimmrPath = Resolve-Path -path "$PSScriptRoot\..\src\Glimmr"
$binPath = Resolve-Path -path "$PSScriptRoot\..\src\Glimmr\bin"
$trayPath = Resolve-Path -path "$PSScriptRoot\..\src\GlimmrTray"
$osxPath = Resolve-Path -path "$PSScriptRoot\..\Glimmr-macos-installer-builder\MacOS-x64\application"
$builderPath = Resolve-Path -path "$PSScriptRoot\..\Glimmr-image-gen\stage2\05-glimmr\files"
$builderPath64 = Resolve-Path -path "$PSScriptRoot\..\Glimmr-image-gen-x64\stage2\05-glimmr\files"

if (-not(Test-Path -Path $innoPath -PathType Leaf)) {
	try {
		Write-Host "Inno setup is not installed, please install it."
		Write-Host "https://jrsoftware.org/isdl.php"
		Exit
	}
	catch {
		throw $_.Exception.Message
	}
} else {
	Write-Host "Inno setup found."
}

foreach ($extension IN $extensions) {
	Remove-Item "$binPath\*.$extension"
}

Set-Location ..\src\Glimmr\

foreach ($target in $targets) {
	write-host Packaging $target
	dotnet publish -r $target -c Release -o "$binPath\Release\net6.0\$target" --self-contained=true
	if($target -like 'win-*') {
		Write-Host "Publishing..."
		dotnet publish -r $target -c Release -o "$binPath\Release\net6.0\$target" --self-contained=true $trayPath\GlimmrTray.csproj
		Write-Host "Creating zip..."
		Invoke-Expression -Command "$PSScriptRoot\7z.exe a -mx9 -tzip -r $binPath\$fullVersion.$target.zip $binPath\Release\net6.0\$target\*"
		Write-Host "Building installer..."
		$innoPath = "C:\Program Files (x86)\Inno Setup 6\iscc.exe"
		$arguments = "/F$fullVersion.$target", "$glimmrPath\build_$target.iss"
		#start new process with argumens and wait.
		Start-Process -FilePath $innoPath -ArgumentList $arguments -Wait
	}
	
	if ($target -like 'linux-*') {
		Write-Host "Creating deb/rpm/tar..."
		dotnet deb -c Release -r $target -o $binPath
		dotnet rpm -c Release -r $target -o $binPath
		dotnet tarball -c Release -r $target -o $binPath
	}
	
	if ($target -like 'linux-arm64'){
		$path = @(Get-ChildItem "$binPath\Glimmr.*.linux-arm64.rpm")[0]
		$outputFile = Split-Path $path -leaf
		$fullVersion = $outputFile.Replace(".linux-arm64.rpm", "")
		$version = $fullVersion.Replace("Glimmr.","")
		Write-Host "Version set to $version"
		Write-Host "Copying x64 ARM package for builder..."
		Copy-Item -Path "$binPath\$fullVersion.$target.tar.gz" -Destination "$builderPath64\archive.tgz"
	}
	
	if ($target -like 'linux-arm') {
		Write-Host "Copying ARM package for builder..."
		Copy-Item -Path "$binPath\$fullVersion.$target.tar.gz" -Destination "$builderPath\archive.tgz"
	}
	
	if ($target -like 'osx-*') {
		dotnet tarball -c Release -o $binPath -r $target
		Write-Host "Copying OSX files for installer builder..."
		Remove-Item $osxPath\* -Recurse
		Copy-Item -Path "$binPath\$target\*" -Destination $glimmrPath -Recurse
	}	
}

Set-Location ..\..\script