#!/bin/sh
echo "Beginning Glimmr setup for OSX."

# Download and extract latest release
cd "$HOME" || exit
echo "Downloading latest release."
ver=$(curl -s "https://api.github.com/repos/d8ahazard/glimmr/releases/latest" | grep '"tag_name":' | sed -E 's/.*"([^"]+)".*/\1/')
echo ver is "$ver"

if [ -d "/Library/Glimmr/" ]
  then
    echo "Bye bye."
    pkill Glimmr
    rm -rf /Library/Glimmr/*
fi

if [ ! -d "/Library/Glimmr/$ver" ]
  then
  echo "Creating app dir."
# Make dir
  mkdir /Library/Glimmr/"$ver"/
fi
url="https://github.com/d8ahazard/glimmr/releases/download/$ver/Glimmr-osx-$ver.tgz"
echo Grabbing archive from "$url"
curl -L -o "$HOME"/archive.tgz "$url"
tar zxvf "$HOME"/archive.tgz -C /Library/Glimmr/"$ver"
chmod -R 777 /Library/Glimmr
if [ ! -d "Library/Application Support/Glimmr" ]
  then
    mkdir "/Library/Application Support/Glimmr"
    chmod 777 "/Library/Application Support/Glimmr"
fi

echo "cp /Library/Glimmr/1.2.0/com.glimmr.plist /Library/LaunchAgents/com.glimmr.plist"
cp /Library/Glimmr/1.2.0/com.glimmr.plist /Library/LaunchAgents/com.glimmr.plist

# As needed through script, logged in user is variable below
loggedInUser=$( ls -l /dev/console | awk '{print $3}' )

# Get loggedInUser ID
userID=$( id -u "$loggedInUser" )

chown root:wheel /Library/LaunchAgents/com.glimmr.plist
chmod 644 /Library/LaunchAgents/com.glimmr.plist

launchctl bootstrap gui/"$userID" /Library/LaunchAgents/com.glimmr.plist

rm "$HOME"/archive.tgz
# shellcheck disable=SC2039
read -n 1 -r -s -p $'Install complete, press enter to continue. You may want to reboot now.\n'