Stop-Process -name "Glimmr" -ErrorAction SilentlyContinue;
Stop-Process -name "GlimmrTray.exe" -ErrorAction SilentlyContinue;

Start-Process -FilePath "$Env:ProgramFiles\Glimmr\GlimmrTray.exe"