param (
	[alias("t")][string]
	#Target device for pushing remotely
	$targetDevice = "", 
	[alias("r")][string]
	# Runtime to compile/push
	$targetRuntime = "all",
	[alias("u")][string]
	# Username for remote commands
	$username = "glimmrtv",
	[alias("p")][string]
	# Password for remote commands
	$password = "glimmrtv",
	[alias("s")][switch]
	# Start/stop services or restart locally
	$service = $false,
	[alias("f")][switch]
	# Push ALL files, default is just "base" files
	$full = $false,
	[alias("c")][switch]
	# Push only CSS files, no restart
	$css = $false,
	[alias("j")][switch]
	# Push only JS Files, no restart
	$javascript = $false,
	[alias("w")][switch]
	# Push all web files, no restart
	$web = $false
)
$targets = "linux-arm64", "linux-arm", "linux-x64", "win-arm64", "win-x64", "win-x86", "osx-x64"
$package = $true
if (-not($targetRuntime -eq "all")){
	$targets = $targetRuntime
	$package = $false
}
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
$baseFiles = "Glimmr.Views.dll", "Glimmr.deps.json", "Glimmr.Views.pdb","wwwroot\js\*","wwwroot\css\*","Glimmr.dll",
"Glimmr.pdb","Glimmr.xml","Glimmr.runtimeconfig.json","Glimmr"

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

if ($targetRuntime -like "win-*") {
	if ($service) {
		Write-Host "Stopping Windows Process..."
		Stop-Process -name "Glimmr" -ErrorAction SilentlyContinue		
	}
}

foreach ($target in $targets) {
	write-host Packaging $target
	dotnet publish -r $target -c Release -o "$binPath\Release\net6.0\$target" --self-contained=true
	if($target -like 'win-*') {
		Write-Host "Publishing..."
		dotnet publish -r $target -c Release -o "$binPath\Release\net6.0\$target" --self-contained=true $trayPath\GlimmrTray.csproj
		if ($package) {
			Write-Host "Creating zip..."
			Invoke-Expression -Command "$PSScriptRoot\7z.exe a -mx9 -tzip -r $binPath\$fullVersion.$target.zip $binPath\Release\net6.0\$targetRuntime\*"
			Write-Host "Building installer..."
			$innoPath = "C:\Program Files (x86)\Inno Setup 6\iscc.exe"
			$arguments = "/F$fullVersion.$target", "$glimmrPath\build_$target.iss"
			#start new process with argumens and wait.
			Start-Process -FilePath $innoPath -ArgumentList $arguments -Wait
		}
	}
	
	if ($target -like 'linux-*' -and $package) {
		Write-Host "Creating deb/rpm/tar..."
		dotnet deb -c Release -r $target -o $binPath
		dotnet rpm -c Release -r $target -o $binPath
		dotnet tarball -c Release -r $target -o $binPath		
	}
	
	if ($target -like 'linux-arm64' -and $package) {
		$path = @(Get-ChildItem "$binPath\Glimmr.*.linux-arm64.rpm")[0]
		$outputFile = Split-Path $path -leaf
		$fullVersion = $outputFile.Replace(".linux-arm64.rpm", "")
		$version = $fullVersion.Replace("Glimmr.","")
		Write-Host "Version set to $version"
		Write-Host "Copying x64 ARM package for builder..."
		Copy-Item -Path "$binPath\$fullVersion.$target.tar.gz" -Destination "$builderPath64\archive.tgz"
	}
	
	if ($target -like 'linux-arm' -and $package) {
		Write-Host "Copying ARM package for builder..."
		Copy-Item -Path "$binPath\$fullVersion.$target.tar.gz" -Destination "$builderPath\archive.tgz"
	}
	
	if ($target -like 'osx-*' -and $package) {
		if ($package) {
			dotnet tarball -c Release -o $binPath -r $target
			Write-Host "Copying OSX files for installer builder..."
			Remove-Item $osxPath\* -Recurse
			Copy-Item -Path "$binPath\$target\*" -Destination $glimmrPath -Recurse
		}
	}	
}

if ($targetDevice -ne "" -or ($service -and $targetRuntime -like "win-*")) {
	Write-Host "Let's push some code."
	if ($service) {
		if ($targetRuntime -like 'osx-*') {
			Write-Host "Stopping OSX service..."
			plink -no-antispoof -pw $password $username@$targetDevice "echo $password | sudo -S pkill -f Glimmr"
		} elseif ($targetRuntime -like 'linux-*') {
			Write-Host "Stopping Linux service..."
			plink -no-antispoof -pw $password "$username@$targetDevice" "echo $password | sudo -S service glimmr stop"
			plink -no-antispoof -pw $password "$username@$targetDevice" "echo $password | sudo -S pkill -f Glimmr"
		}
	}
	
	if ($targetRuntime -like 'linux-*' -or $targetRuntime -like 'osx-*') {
		if ($targetRuntime -like 'linux-*') {
			$targetPath = '/usr/share/Glimmr'
		} else {
			$targetPath = "/Library/Glimmr/$version"
		}
		
		if (-not ($full)) {
			if ($css) {
				pscp -P 22 -r -pw $password $binPath\Release\net6.0\$targetRuntime\wwwroot\css\* "${username}@${targetDevice}:$targetPath/wwwroot/css/"
			} elseif ($javascript) {
				pscp -P 22 -r -pw $password $binPath\Release\net6.0\$targetRuntime\wwwroot\js\* "${username}@${targetDevice}:$targetPath/wwwroot/js/"
			} elseif ($web) {
				pscp -P 22 -r -pw $password $binPath\Release\net6.0\$targetRuntime\wwwroot\* "${username}@${targetDevice}:$targetPath/wwwroot/"
			} else {
				foreach($file in $baseFiles) {
					pscp -P 22 -r -pw $password $binPath\Release\net6.0\$targetRuntime\$file "${username}@${targetDevice}:$targetPath/$file"
				}
			}
		} else {
			pscp -P 22 -r -pw $password $binPath\Release\net6.0\$targetRuntime\* "${username}@${targetDevice}:$targetPath/"	
		}		
	}
	
	if ($service -and -not ($web -and $css -and $js)) {
		if ($targetRuntime -like 'osx-*') {
			Write-Host "Restarting Glimmr on OSX"
			plink -no-antispoof -pw $password "$username@$targetDevice" "echo $password | sudo launchctl load -w /Library/LaunchAgents/com.glimmr.plist"
		} elseif ($targetRuntime -like 'linux-*'){
			Write-Host "Restarting Glimmr on Linux"
			plink -no-antispoof -pw $password "$username@$targetDevice" "echo $password | sudo -S service glimmr start"
		} else {
			Write-Host "Restarting Glimmr on Windoze"
			Start-Process "$binPath\Release\net6.0\$targetRuntime\Glimmr.exe"
		}
	}
}

Set-Location ..\..\script