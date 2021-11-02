#!/bin/sh
echo "Beginning Glimmr setup for OSX."

. /etc/rc.common
dscl . create /Users/glimmrtv
dscl . create /Users/glimmrtv RealName "Glimmr TV User"
dscl . create /Users/glimmrtv hint "It's your username."
dscl . create /Users/glimmrtv picture "/Application/glimmr/wwwroot/apple-icon-144x144.png"
dscl . passwd /Users/glimmrtv glimmrtv
dscl . create /Users/glimmrtv UniqueID 420
dscl . create /Users/glimmrtv PrimaryGroupID 20
dscl . create /Users/glimmrtv UserShell /bin/bash
dscl . create /Users/glimmrtv NFSHomeDirectory /Users/glimmrtv
cp -R /System/Library/User\ Template/English.lproj /Users/glimmrtv
chown -R glimmrtv:staff /Users/glimmrtv

if [ ! -f "/Users/glimmrtv/firstrun" ]
then
  echo "Starting first-run setup..."
  touch /Users/glimmrtv/firstrun
fi

if [ ! -d "/Applications/glimmr" ]
  then
  echo "Creating app dir."
# Make dir
  mkdir /Applications/glimmr
fi

# Download and extract latest release
cd /Users/glimmrtv || exit
echo "Downloading latest release."
ver=$(curl -s "https://api.github.com/repos/d8ahazard/glimmr/releases/latest" | grep '"tag_name":' | sed -E 's/.*"([^"]+)".*/\1/')
echo ver is $ver
url="https://github.com/d8ahazard/glimmr/releases/download/$ver/Glimmr-osx-$ver.tgz"
echo Grabbing archive from $url
curl -oL ./archive.tgz $url
tar zxvf ./archive.tgz -C /Applications/glimmr
chmod -R 777 /Applications/glimmr
rm ./archive.tgz

read -n 1 -r -s -p $'Install complete, press enter to continue. You may want to reboot now.\n'