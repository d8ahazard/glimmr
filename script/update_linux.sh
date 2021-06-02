#!/bin/bash
log=$(ls -t /var/log/glimmr/glimmrupdatelog* | head -1)

#Stop service
echo "Stopping glimmr..." >> $log
service glimmr stop
echo "SERVICE STOPPED!" >> $log

# Fetch changes from github repo
cd /home/glimmrtv/glimmr || exit
git fetch && git pull >> $log

# Build latest version
echo "Building glimmr..." >> $log
/opt/dotnet/dotnet publish /home/glimmrtv/glimmr/src/Glimmr.csproj /p:PublishProfile=Linux -o /home/glimmrtv/glimmr/bin/
echo "DONE." >> $log

#Give all scripts full permission
echo "Restarting..." >> $log

# Restart Service
service glimmr start
