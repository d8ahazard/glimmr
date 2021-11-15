#!/bin/sh
echo "Beginning Glimmr update for OSX."

if [ ! -d "/Applications/glimmr" ]
  then
  echo "Creating app dir."
# Make dir
  mkdir /Applications/glimmr
fi

# Download and extract latest release
cd $HOME || exit
echo "Downloading latest release."
ver=$(curl -s "https://api.github.com/repos/d8ahazard/glimmr/releases/latest" | grep '"tag_name":' | sed -E 's/.*"([^"]+)".*/\1/')
echo ver is $ver
url="https://github.com/d8ahazard/glimmr/releases/download/$ver/Glimmr-osx-$ver.tgz"
echo Grabbing archive from $url
curl -L -o $HOME/archive.tgz $url
tar zxvf $HOME/archive.tgz -C /Applications/glimmr
chmod -R 777 /Applications/glimmr
rm $HOME/archive.tgz

read -n 1 -r -s -p $'Install complete, press enter to continue. You may want to reboot now.\n'