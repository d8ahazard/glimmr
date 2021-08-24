#!/bin/bash
branch=${1:-"master"}

PUBPROFILE="Linux";
if [ -f "/usr/bin/raspi-config" ] 
  then
    PUBPROFILE="LinuxARM"
fi

unameOut="$(uname -s)"
if [ $unameOut == "Darwin" ]
  then
    PUBPROFILE="OSX"
fi

if [ $unameOut == "FreeBSD" ]
  then
    PUBPROFILE="Portable"
fi


echo "Updating Glimmr for $PUBPROFILE using branch $branch."

log=$(ls -t /var/log/glimmr/glimmr* | head -1)

#Stop service
echo "Stopping glimmr..." >> $log
service glimmr stop
echo "SERVICE STOPPED!" >> $log

# Fetch changes from github repo
cd /home/glimmrtv/glimmr || exit
git checkout $branch >> $log
git fetch && git pull >> $log

# Build latest version
echo "Building glimmr using profile $PUBPROFILE..." >> $log
/opt/dotnet/dotnet publish /home/glimmrtv/glimmr/src/Glimmr.csproj /p:PublishProfile=$PUBPROFILE -o /home/glimmrtv/glimmr/bin/
if [ -d "/home/glimmrtv/glimmr/lib/$PUBPROFILE/" ]
  then
    cp -r /home/glimmrtv/glimmr/lib/$PUBPROFILE/* /usr/lib
fi
cp -r /home/glimmrtv/glimmr/lib/bass.dll /usr/lib/bass.dll
#Give all scripts full permission
chmod -R 777 /home/glimmrtv/glimmr/script
chmod -R 777 /home/glimmrtv/glimmr/bin
echo "DONE." >> $log

echo "Restarting glimmr service..." >> $log

# Restart Service
service glimmr start
