#!/bin/sh
# Set script variables

PUBPROFILE="OSX"
PUBFILE="osx"
APPDIR="/Applications/glimmr"
HOMEDIR="/Users/glimmrtv"
USER="glimmrtv"

echo "Beginning Glimmr setup/update for $PUBPROFILE"

. /etc/rc.common
dscl . create /Users/$USER
dscl . create /Users/$USER RealName "Glimmr TV User"
dscl . create /Users/$USER hint "It's your username."
dscl . create /Users/$USER picture "/Application/glimmr/wwwroot/apple-icon-144x144.png"
dscl . passwd /Users/$USER glimmrtv
dscl . create /Users/$USER UniqueID 420
dscl . create /Users/$USER PrimaryGroupID 20
dscl . create /Users/$USER UserShell /bin/bash
dscl . create /Users/$USER NFSHomeDirectory /Users/$USER
cp -R /System/Library/User\ Template/English.lproj /Users/$USER
chown -R $USER:staff /Users/$USER

if [ ! -f "$HOMEDIR/firstrun" ]
then
  echo "Starting first-run setup..."
  echo "done" > "$HOMEDIR/firstrun"
fi

# Check for service stop
serviceName="glimmr"
if systemctl --all --type service | grep -q "$serviceName";then
  echo "Stopping glimmr..."
    service glimmr stop
    echo "STOPPED!"
else
    echo "$serviceName is not installed."
fi

if [ ! -d $APPDIR ]
  then
# Make dir
  mkdir $APPDIR  
fi

# Download and extract latest release
cd /tmp || exit
ver=$(wget "https://api.github.com/repos/d8ahazard/glimmr/releases/latest" -q -O - | grep '"tag_name":' | sed -E 's/.*"([^"]+)".*/\1/')
echo ver is $ver
url="https://github.com/d8ahazard/glimmr/releases/download/$ver/Glimmr-$PUBPATH-$ver.tgz"
echo Grabbing archive from $url
wget -O archive.tgz $url
tar zxvf ./archive.tgz -C $APPDIR
chmod -R 777 $APPDIR
rm ./archive.tgz
echo "DONE." >> $log

# Check service start/install
if systemctl --all --type service | grep -q "$serviceName";then
  echo "Starting Glimmr service..."
  systemctl daemon-reload
  service glimmr start
  echo "DONE!"
else  
  echo "$serviceName does NOT exist, installing."
  echo "
[Unit]
Description=GlimmrTV

[Service]
Type=simple
RemainAfterExit=yes
StandardOutput=tty
Restart=always
User=root
WorkingDirectory=$APPDIR
ExecStart=sudo Glimmr
KillMode=process

[Install]
WantedBy=multi-user.target

" > /etc/systemd/system/glimmr.service
  systemctl daemon-reload
  systemctl enable glimmr.service
  systemctl start glimmr.service
fi
read -n 1 -r -s -p $'Install complete, press enter to continue. You may want to reboot now.\n'