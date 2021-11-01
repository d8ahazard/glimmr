#!/bin/bash
profile=${1:-"Linux"}
if [ -f "/usr/bin/raspi-config" ] 
  then
    profile="LinuxARM"
    cp ./rules_linux_arm ../src/Glimmr/debian/rules
else 
  cp ./rules_linux ../src/Glimmr/debian/rules
fi
cd ../src/Glimmr
echo Building package for $profile
dpkg-buildpackage -b --no-sign
cd ../../script