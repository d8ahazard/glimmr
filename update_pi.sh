#!/bin/bash
#Update glimmr
cd /home/glimmrtv/glimmr
git stash && git fetch && git clone
cd /home/glimmrtv/glimmr/src || exit

service glimmr stop
echo "SERVICE STOPPED!"

# Build latest version
echo "Building glimmr..."
dotnet build Glimmr.csproj /p:PublishProfile=LinuxARM
cp -r /home/glimmrtv/glimmr/src/bin/debug/net5.0/linux-arm/* /home/glimmrtv/glimmr/
cp -r /home/glimmrtv/glimmr/src/wwwroot/ /home/glimmrtv/glimmr/wwwroot/
echo "DONE."
# Copy necessary libraries
echo "Copying libs..."
cp -r /home/glimmrtv/glimmr/src/build/arm /usr/lib
service glimmr start