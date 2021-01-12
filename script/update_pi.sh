#!/bin/bash
#Update glimmr
log=$(ls -t /var/log/glimmr/glimmr* | head -1)
cd /home/glimmrtv/glimmr || exit
service glimmr stop
echo "SERVICE STOPPED!" >> $log
git fetch && git pull
# Build latest version
echo "Building glimmr..." >> $log
dotnet publish ./src/Glimmr.csproj /p:PublishProfile=LinuxARM -o ./bin/
echo "DONE." >> $log
# Copy necessary libraries
echo "Copying libs..." >> $log
cp -r /home/glimmrtv/glimmr/lib/arm/* /usr/lib
chmod 777 ./script/*.sh
echo "Restarting..." >> $log
service glimmr start