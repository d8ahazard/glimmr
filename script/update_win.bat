@echo off
cd ..
git fetch && git pull
net stop glimmr
echo Publishing for windows
set version=1.1.0
dotnet publish .\src\Glimmr.csproj /p:PublishProfile=Windows -o .\bin\
echo copying bass.dll from .\lib\win\bass.dll to .\bin\bass.dll
copy .\lib\win\bass.dll .\bin\bass.dll
net start glimmr
