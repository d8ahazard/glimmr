#!/bin/bash
#Update glimmr
log=$(ls -t glimmr* | head -1)
cd /home/glimmrtv/glimmr || exit
service glimmr stop
echo "SERVICE STOPPED!" >> $log
git fetch && git pull
# Build latest version
echo "Building glimmr..." >> $log
dotnet publish ./src/Glimmr.csproj /p:PublishProfile=LinuxARM -o ./bin/
# cp -r /home/glimmrtv/glimmr/bin/Debug/net5.0/linux-arm/* /home/glimmrtv/glimmr/
echo "DONE." >> $log
# Copy necessary libraries
echo "Copying libs..." >> $log
cp -r /home/glimmrtv/glimmr/lib/arm/* /usr/lib
chmod 777 ./*.sh
echo "Restarting" >> $log
service glimmr start