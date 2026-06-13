#!/bin/bash
# Glimmr manual installer / updater for Linux.
#
# Notes:
# - The recommended way to run Glimmr on a Raspberry Pi is the prebuilt OS
#   image (Glimmr-image-gen-x64), which also handles Pi 5 LED bring-up.
# - This script installs runtime dependencies only; Glimmr ships as a
#   self-contained .NET 8 publish, so no -dev packages are required.
set -u

PUBPROFILE="Linux"
PUBFILE="linux-x64"
env=${1:-"normal"}
unameOut="$(uname -s)"
arch="$(uname -m)"

PI_MODEL=""
if [ -r /proc/device-tree/model ]; then
  PI_MODEL=$(tr -d '\0' < /proc/device-tree/model)
fi

IS_PI=0
case "$PI_MODEL" in
  *"Raspberry Pi"*) IS_PI=1 ;;
esac
if [ -f "/usr/bin/raspi-config" ]; then
  IS_PI=1
fi

if [ "$IS_PI" == "1" ] && { [ "$arch" == "armv7l" ] || [ "$arch" == "armv71" ]; }; then
  PUBPROFILE="LinuxARM"
  PUBFILE="linux-arm"
fi

if [ "$IS_PI" == "1" ] && [ "$arch" == "aarch64" ]; then
  PUBPROFILE="LinuxARM64"
  PUBFILE="linux-arm64"
fi

if [ "$unameOut" == "Darwin" ]; then
  echo "Please use the setup_osx.sh script for OSX installation."
  exit 0
fi

if [ "$unameOut" == "FreeBSD" ]; then
  PUBPROFILE="Portable"
fi

# Bookworm moved the boot partition mount to /boot/firmware.
BOOT_CONFIG="/boot/config.txt"
if [ -f "/boot/firmware/config.txt" ]; then
  BOOT_CONFIG="/boot/firmware/config.txt"
fi

echo "Performing cleanup..."

if command -v systemctl &> /dev/null; then
  serviceName="glimmr"
  if systemctl --all --type service | grep -q "$serviceName"; then
    echo "Stopping old glimmr service..."
    systemctl stop glimmr.service || true
  else
    echo "$serviceName is not installed."
  fi
fi

for legacyDir in /opt/glimmr /home/glimmrtv/glimmr /home/glimmrtv/ws281x /opt/dotnet; do
  if [ -d "$legacyDir" ]; then
    echo "Removing legacy $legacyDir folder."
    rm -r "$legacyDir"
  fi
done

echo "Beginning Glimmr setup/update for $PUBPROFILE"

# Runtime dependencies. These mirror Glimmr-image-gen-x64/stage2/05-glimmr/01-packages:
# Emgu.CV's bundled OpenCV links against these at runtime.
RUNTIME_DEPS="avahi-daemon libnss-mdns alsa-utils ffmpeg libavcodec-extra \
libgstreamer1.0-0 libgtk-3-0 xauth x11-xserver-utils curl wget git"

# Versioned runtime libs differ between Debian releases; resolve what's available.
for candidate in \
  "libasound2t64 libasound2" \
  "libjpeg62-turbo libjpeg62" \
  "libpng16-16t64 libpng16-16" \
  "libtiff6 libtiff5" \
  "libwebp7 libwebp6" \
  "libopenexr-3-1-30 libopenexr25" \
  "libtesseract5 libtesseract4" \
  "libgeotiff5 libgeotiff2" \
  "libtbb12 libtbb2" \
  "libatlas3-base" \
  "libdc1394-25 libdc1394-22" \
  "libv4l-0t64 libv4l-0" \
  "libicu76 libicu72 libicu71 libicu67 libicu66"; do
  for pkg in $candidate; do
    if apt-cache show "$pkg" &> /dev/null; then
      RUNTIME_DEPS="$RUNTIME_DEPS $pkg"
      break
    fi
  done
done

echo "Installing runtime dependencies..."
apt-get update
# shellcheck disable=SC2086
apt-get -y install $RUNTIME_DEPS

if [ "$IS_PI" == "1" ]; then
  case "$PI_MODEL" in
    *"Raspberry Pi 5"*|*"Compute Module 5"*)
      # Pi 5 routes GPIO through the RP1 chip: GPIO 19 PWM is ALT3 and WS281x
      # output additionally requires the rp1_ws281x_pwm kernel module, which is
      # built into the official Glimmr OS image. Manual installs must build it
      # from https://github.com/jgarff/rpi_ws281x (branch pi5) themselves.
      if ! grep -q "gpio=19=op,a3" "$BOOT_CONFIG"; then
        printf '\n[pi5]\ngpio=19=op,a3\n[all]\n' >> "$BOOT_CONFIG"
      fi
      echo "NOTE: Pi 5 detected. LED output needs the rp1_ws281x_pwm kernel module;"
      echo "      use the prebuilt Glimmr OS image for turn-key Pi 5 LED support."
      ;;
    *)
      # Pi 3/4: GPIO 19 PWM is ALT5.
      if ! grep -q "gpio=19=op,a5" "$BOOT_CONFIG"; then
        printf '\n[pi3]\ngpio=19=op,a5\n[pi4]\ngpio=19=op,a5\n[all]\n' >> "$BOOT_CONFIG"
      fi
      ;;
  esac
  echo "Raspi boot config updated ($BOOT_CONFIG)."
fi

# Cleanup
apt-get autoclean -y && apt-get autoremove -y

if [ ! -d "/usr/share/Glimmr" ]; then
  mkdir -p /usr/share/Glimmr
fi

# Download and extract latest release
cd /tmp || exit
ver=$(wget "https://api.github.com/repos/d8ahazard/glimmr/releases/latest" -q -O - | grep '"tag_name":' | sed -E 's/.*"([^"]+)".*/\1/')
echo ver is "$ver"
url="https://github.com/d8ahazard/glimmr/releases/download/$ver/Glimmr.$ver.$PUBFILE.tar.gz"
echo Grabbing archive from "$url"
wget -O archive.tgz "$url"
rm -rf /usr/share/Glimmr/*
tar zxvf ./archive.tgz -C /usr/share/Glimmr/
chown -R 0:0 /usr/share/Glimmr
find /usr/share/Glimmr -type d -exec chmod 755 {} \;
find /usr/share/Glimmr -type f -exec chmod 644 {} \;
if [ -f /usr/share/Glimmr/Glimmr ]; then
  chmod 755 /usr/share/Glimmr/Glimmr
fi
rm ./archive.tgz

if [ "$env" == "normal" ]; then
  echo "Installing glimmr.service..."
  # Matches Glimmr-image-gen-x64/stage2/05-glimmr/files/glimmr.service.
  # Run as root, direct ExecStart (no sudo wrapper - it deadlocks on
  # Bookworm where sudo prompts for a password by default).
  cat > /etc/systemd/system/glimmr.service <<'EOF'
[Unit]
Description=GlimmrTV ambient lighting service
Documentation=https://github.com/d8ahazard/glimmr
After=network-online.target glimmr-pi5-leds.service
Wants=network-online.target

[Service]
Type=simple
User=root
WorkingDirectory=/usr/share/Glimmr
ExecStart=/usr/share/Glimmr/Glimmr
Restart=always
RestartSec=5
StandardOutput=journal
StandardError=journal
KillMode=process
TimeoutStopSec=15

# Glimmr drives WS281x LEDs via /dev/mem/PWM and binds to privileged ports
# for device discovery. It also needs raw network access for SSDP/WLED.
AmbientCapabilities=CAP_SYS_RAWIO CAP_NET_BIND_SERVICE CAP_NET_RAW CAP_NET_ADMIN

[Install]
WantedBy=multi-user.target
EOF
  systemctl daemon-reload
  systemctl enable glimmr.service
  systemctl start glimmr.service
fi

read -n 1 -r -s -p $'Install complete, press enter to continue.\n'
