#!/bin/bash
#Update glimmr
cd /home/glimmrtv/glimmr || exit
service glimmr stop
echo "SERVICE STOPPED!"

git stash && git fetch && git clone


# Build latest version
echo "Building glimmr..."
dotnet build Glimmr.csproj /p:PublishProfile=LinuxARM
cp -r /home/glimmrtv/glimmr/bin/Debug/net5.0/linux-arm/* /home/glimmrtv/glimmr/
echo "DONE."
# Copy necessary libraries
echo "Copying libs..."
cp -r /home/glimmrtv/glimmr/build/arm/* /usr/lib
service glimmr start