#!/bin/bash

set -fb

readonly THISDIR=$(cd "$(dirname "$0")" ; pwd)
readonly MY_NAME=$(basename "$0")
readonly FILE_TO_FETCH_URL="https://raw.githubusercontent.com/d8ahazard/glimmr/master/src/Glimmr/script/update_linux.sh"
readonly EXISTING_SHELL_SCRIPT="${THISDIR}/update_linux.sh"
readonly EXECUTABLE_SHELL_SCRIPT="${THISDIR}/.update_linux.sh"
arch="$(arch)"
PUBPROFILE="Linux"
PUBPATH="linux"
log=$(ls -t /var/log/glimmr/glimmr* | head -1)
if [ "$log" == "" ]
  then
    log=/var/log/glimmr/glimmr.log
fi

if [ ! -f $log ]
  then
    log=/var/log/glimmr/glimmr.log
    touch $log
    chmod 777 $log
fi


function get_remote_file() {
  readonly REQUEST_URL=$1
  readonly OUTPUT_FILENAME=$2
  readonly TEMP_FILE="${THISDIR}/tmp.file"
  if [ -n "$(which wget)" ]; then
    echo "Fetching updated script." >> $log
    echo "Fetching updated script."
    $(wget -O "${TEMP_FILE}"  "$REQUEST_URL" 2>&1)
    if [[ $? -eq 0 ]]; then
      mv "${TEMP_FILE}" "${OUTPUT_FILENAME}"
      chmod 755 "${OUTPUT_FILENAME}"
    else
      return 1
    fi
  fi
}

function clean_up() {
  # clean up code (if required) that has to execute every time here
}

function self_clean_up() {
  rm -f "${EXECUTABLE_SHELL_SCRIPT}"
}

function update_self_and_invoke() {
  get_remote_file "${FILE_TO_FETCH_URL}" "${EXECUTABLE_SHELL_SCRIPT}"
  if [ $? -ne 0 ]; then
    cp "${EXISTING_SHELL_SCRIPT}" "${EXECUTABLE_SHELL_SCRIPT}"
  fi
  exec "${EXECUTABLE_SHELL_SCRIPT}" "$@"
}
function main() {
  cp "${EXECUTABLE_SHELL_SCRIPT}" "${EXISTING_SHELL_SCRIPT}"
  

  if [ -f "/usr/bin/raspi-config" ] && [ "$arch" == "armv71" ] 
    then
      PUBPROFILE="LinuxARM"
      PUBPATH="linux-arm"
  fi
  
  if [ -f "/usr/bin/raspi-config" ] && [ "$arch" == "aarch64" ] 
    then
      PUBPROFILE="LinuxARM64"
      PUBPATH="linux-arm64"
  fi
  
  echo "Checking for Glimmr updates for $PUBPROFILE." >> $log
  echo "Checking for Glimmr updates for $PUBPROFILE."
  
  if [ ! -d "/usr/share/Glimmr" ]
    then
  # Make dir
    mkdir /usr/share/Glimmr  
  fi
  
  # Download and extract latest release
  ver=$(wget "https://api.github.com/repos/d8ahazard/glimmr/releases/latest" -q -O - | grep '"tag_name":' | sed -E 's/.*"([^"]+)".*/\1/')
  echo "Repo version is $ver." >> $log
  echo "Repo version is $ver."
  if [ -f "/etc/Glimmr/version" ]
    then
      curr=$(head -n 1 /etc/Glimmr/version)
      echo "Current version is $curr." >> $log
      echo "Current version is $curr."
      diff=$(vercomp "$curr" "$ver")
      if [ "$diff" != "2" ]
        then
          echo "Nothing to update, diff is $diff." >> $log
          echo "Nothing to update, diff is $diff."
          exit 0
      fi
  fi
  
  cd /tmp || exit
  echo "Updating glimmr to version $ver." >> $log
  echo "Updating glimmr to version $ver."
  url="https://github.com/d8ahazard/glimmr/releases/download/$ver/Glimmr-$PUBPATH-$ver.tgz"
  echo "Grabbing archive from $url" >> $log
  echo "Grabbing archive from $url"
  wget -O archive.tgz "$url"
  #Stop service
  echo "Stopping glimmr services..." >> $log
  echo "Stopping glimmr services..."
  service glimmr stop
  echo "Services stopped." >> $log
  echo "Services stopped."
  echo "Extracting archive..." >> $log
  echo "Extracting archive..."
  tar zxvf ./archive.tgz -C /usr/share/Glimmr/
  echo "Setting permissions..." >> $log
  echo "Setting permissions..."
  chmod -R 777 /usr/share/Glimmr/
  echo "Cleanup..." >> $log
  echo "Cleanup..."
  rm ./archive.tgz
  echo "Update completed." >> $log
  echo "Update completed."
  echo "$ver" > /etc/Glimmr/version
  echo "Restarting glimmr service..." >> $log
  echo "Restarting glimmr service..."
  
  # Restart Service
  service glimmr start
} 

function vercomp () {
    if [[ "$1" == "$2" ]]
    then
        echo "0"
        return 0
    fi
    local IFS=.
    local i ver1=("$1") ver2=("$2")
    # fill empty fields in ver1 with zeros
    for ((i=${#ver1[@]}; i<${#ver2[@]}; i++))
    do
        ver1[i]=0
    done
    for ((i=0; i<${#ver1[@]}; i++))
    do
        if [[ -z ${ver2[i]} ]]
        then
            # fill empty fields in ver2 with zeros
            ver2[i]=0
        fi
        if ((10#${ver1[i]} > 10#${ver2[i]}))
        then
            echo "1"
            return 1
        fi
        if ((10#${ver1[i]} < 10#${ver2[i]}))
        then
            echo "2"
            return 2
        fi
    done
    echo "0"
    return 0
}

if [[ $MY_NAME = \.* ]]; then
  # invoke real main program
  trap "clean_up; self_clean_up" EXIT
  main "$@"
else
  # update myself and invoke updated version
  trap clean_up EXIT
  update_self_and_invoke "$@"
fi