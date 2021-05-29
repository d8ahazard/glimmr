#!/bin/bash
log=$(ls -t /var/log/glimmr/glimmr* | head -1)

#Stop service
service glimmr stop
echo "SERVICE STOPPED!" >> $log

# Fetch changes from github repo
cd /home/glimmrtv/glimmr || exit
git fetch && git pull

# Build latest version
echo "Building glimmr..." >> $log
/opt/dotnet/dotnet publish /home/glimmrtv/glimmr/src/Glimmr.csproj /p:PublishProfile=Linux -o /home/glimmrtv/glimmr/bin/
echo "DONE." >> $log

#Give all scripts full permission
chmod 777 ./script/*.sh
echo "Restarting..." >> $log

# Restart Service
service glimmr start
