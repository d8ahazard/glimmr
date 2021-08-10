@echo off
if [%1]==[] goto SetBranch
set branch=%1
goto BranchSet
:SetBranch
set branch=master
:BranchSet
cd ..
git stash
git checkout %branch%
git fetch && git pull
taskkill /IM GlimmrTray.exe /F
taskkill /IM Glimmr.exe /F
echo Publishing for windows
set version=1.1.7
dotnet publish .\src\Glimmr.csproj /p:PublishProfile=Windows -o .\bin\
echo copying bass.dll from .\lib\win\bass.dll to .\bin\bass.dll
copy .\lib\win\bass.dll .\bin\bass.dll
C:\progra~1\glimmr\bin\GlimmrTray.exe
