@echo off
dotnet publish .\src\Glimmr.csproj /p:PublishProfile=Windows -o .\bin
copy .\lib\win\bass.dll .\bin\bass.dll

cd .\bin\publish\

