# [d8ahazard/glimmr](https://github.com/d8ahazard/glimmr)

## What the heck is this??
Glimmr is a FOSS ambient home lighting solution. Hook it up to a HDMI signal, use a webcam, or use the screen capture function; and drive a wide range of lighting devices in sync with the input signal. It's similar to Hyperion, Govee, or the Hue Sync box, only with a much broader support for devices.

The project is written in dotnet core, which means it can run on most any modern arm, intel, or AMD processor. Presently, it has been tested to work with Debian and Ubuntu linux, as well as raspbian and Windows 10 and 11. It is specifically designed to run on a raspberry Pi 4B, however, it has also been shown to run fine on a 3B, and could potentailly also work on a zero...although it has not been tested.

Supported devices include DreamScreen, Hue, Lifx, Nanoleaf, WLED, Adalight (Arduino), Yeelight, and WS2812B/SK6822 strips connected to the GPIO of a raspberry pi. Additionally, a *vast* array of desktop RGB devices are supported via OpenRGB integration. And, if there's not a device supported and it has an API, I'm more than willing to try adding support for it.

In addition to all the supported devices, each device has a custom set of options which can be used to ensure perfect alignement with the screen content, regardless of room placement. Mirroring, scaling, and brightness are all configurable for every device.

Additional features include automatic updates, black bar detection, auto-disable and enable; audio, audio/video, and ambient modes with user-defineable parameters via a JSON loading system.

The app is controllable via an inbuilt web interface, Android application (available on the play store), and a fully documented API (via swagger). Python api wrapper WIP.

<img src="https://github.com/d8ahazard/glimmr/blob/dev/docs/logos/arduino.png?raw=true" width=100 height=100><img src="https://github.com/d8ahazard/glimmr/blob/dev/docs/logos/debian.png?raw=true" width=100 height=100><img src="https://github.com/d8ahazard/glimmr/blob/dev/docs/logos/docker.png?raw=true" width=100 height=100><img src="https://github.com/d8ahazard/glimmr/blob/dev/docs/logos/dreamscreen.png?raw=true" width=100 height=100><img src="https://github.com/d8ahazard/glimmr/blob/dev/docs/logos/hue.png?raw=true" width=100 height=100><img src="https://github.com/d8ahazard/glimmr/blob/dev/docs/logos/lifx.png?raw=true" width=100 height=100><img src="https://github.com/d8ahazard/glimmr/blob/dev/docs/logos/linux.png?raw=true" width=100 height=100><img src="https://github.com/d8ahazard/glimmr/blob/dev/docs/logos/nanoleaf.png?raw=true" width=100 height=100><img src="https://github.com/d8ahazard/glimmr/blob/dev/docs/logos/openrgb.png?raw=true" width=100 height=100><img src="https://github.com/d8ahazard/glimmr/blob/dev/docs/logos/raspi.png?raw=true" width=100 height=100><img src="https://github.com/d8ahazard/glimmr/blob/dev/docs/logos/ubuntu.png?raw=true" width=100 height=100><img src="https://github.com/d8ahazard/glimmr/blob/dev/docs/logos/windows.png?raw=true" width=100 height=100><img src="https://github.com/d8ahazard/glimmr/blob/dev/docs/logos/wled.png?raw=true" width=100 height=100><img src="https://github.com/d8ahazard/glimmr/blob/dev/docs/logos/yeelight.png?raw=true" width=100 height=100>

## Installation

### Windows

#### Installer
Download the latest .exe installer from [releases](https://github.com/d8ahazard/glimmr/releases/latest), and run it.


#### Scripted
Open a Powershell window, execute the following command:

```
iex ((New-Object System.Net.WebClient).DownloadString('https://raw.githubusercontent.com/d8ahazard/glimmr/dev/script/setup_win.ps1'))

```

OR, to clone from the master branch:
```
iex ((New-Object System.Net.WebClient).DownloadString('https://raw.githubusercontent.com/d8ahazard/glimmr/master/script/setup_win.ps1'))
```
Once the script is done running, you should now have a "GlimmrTray" application in your start menu.

Click this to launch Glimmr, minimize the console window to have it stored in the tray.

Note: Glimmr/GlimmrTray *MUST* be run as administrator in order for screen capture to work.

### Raspberry Pi

#### Installer
Download the latest linux-arm deb package installer from [releases](https://github.com/d8ahazard/glimmr/releases/latest), and 
installing it by running ```apt-get install FILENAME.deb```.

#### Script
Execute the following command:
```
sudo su
wget -qO- https://raw.githubusercontent.com/d8ahazard/glimmr/dev/script/setup_linux.sh | bash
```

*Alternatively*, you can flash a custom image directly to your pi from here:

https://mega.nz/file/m2RHyIAI#wFZqNlS3zxf2WnJChmUHgCMHsBejjLkFRJ6o8Na0a8w

You will need to use "BalenaEtcher", a free software for flashing the image.

https://www.balena.io/etcher/

Once Balena is installed and loaded, select the image file you downloaded from above, and make sure your
micro SD card is connected to your computer. Select the SD card, and click "Flash" to begin.

Once flashing is done - simply insert the SD card into your pi and boot it up.

If using the custom RasPi image, it is configured to use the Comitup service to create an access point which
you can connect to to configure your Glimmr's wifi settings. Once the pi is booted, use a phone or laptop and
connect to the access point "Comitup-XXX" or "Glimmr-XXX" (WIP). Once connected, you should be prompted to select
a local wifi network and enter a password. Make a note of the name of the access point.

Once wifi is configured, reconnect to your home wifi, and enter http://glimmr-xxx into a browser, where the -xxx is
the same as the access point.

Note: Glimmr is installed as a service, and can be stopped/started by running "sudo service glimmr start"
or "sudo service glimmr stop" respectively.



### Linux

#### Installer
Download the latest linux-x64 deb package installer from [releases](https://github.com/d8ahazard/glimmr/releases/latest), and
installing it by running ```apt-get install FILENAME.deb```.

#### Script
Execute the following command. You can replace "dev" with "master" to use the master branch instead.:
```
sudo su
wget -qO- https://raw.githubusercontent.com/d8ahazard/glimmr/dev/script/setup_linux.sh | bash
```

OR, to clone from the master branch...

```
sudo su
wget -qO- https://raw.githubusercontent.com/d8ahazard/glimmr/master/script/setup_linux.sh | bash
```

Note: Glimmr is installed as a service, and can be stopped/started by running "sudo service glimmr start"
or "sudo service glimmr stop" respectively.


### OSX
Open a terminal, execute the following commands:
```
sudo su
curl https://raw.githubusercontent.com/d8ahazard/glimmr/dev/script/setup_osx.sh | sh
```

### docker
Use the following command. You don't need to specify the ASPNETCORE_URLS value, unless you wish to change the default
port that the web UI listens on. If so, modify the port number. e.g.: 'http://+:5699' to 'http://+:80'

```
docker create \
  --name=glimmr \
  -v <path to data>:/etc/glimmr \
  -p 1900:1900/udp \
  -p 2100:2100/udp \
  -p 5353:5353/udp \  
  -p 8888:8888/udp \ 
  -p 56700:56700/udp \ 
  -p 60222:60222/udp \ 
  -p 80:5699 \
  -p 443:5670 \
  --network="bridge" \
  --restart unless-stopped \
  digitalhigh/glimmr
```


### docker-compose

Compatible with docker-compose v2 schemas.

```
---
version: "2"
services:
  glimmr:
    image: d8ahazard/glimmr
    container_name: glimmr
    restart: unless-stopped
    network: bridge
	volumes:
      	- <path to data>:/etc/glimmr
    ports:
      	- 1900:1900/udp
  	- 2100:2100/udp
  	- 5353:5353/udp
  	- 8888:8888/udp
  	- 56700:56700/udp
  	- 60222:60222/udp
  	- 5699:5699
```

#### Parameters

Container images are configured using parameters passed at runtime (such as those above). These parameters are separated by a colon and indicate `<external>:<internal>` respectively. For example, `-p 8080:80` would expose port `80` from inside the container to be accessible from the host's IP on port `8080` outside the container.

| Parameter | Function |
| :----: | --- |
| `-v <path_to_data>/etc/glimmr` | Change <path_to_data> to the location where to keep glimmr ini |
| `-p 1900:1900\udp` | Hue Discovery port |
| `-p 2100:2100\udp` | Hue Broadcast port |
| `-p 5353:5353\udp` | MDNS Discovery port |
| `-p 8888:8888\udp` | DS Emulation port |
| `-p 56700:56700\udp` | LIFX Discovery port |
| `-p 60222:5353\udp` | Nanoleaf Discovery port |
| `-p 5699:5699` | Web UI port |
| `-network="bridge"` | Because Glimmr requires MDNS for discovery, it is recommended to use a bridge when running this container, otherwise a binding error on port 5353 is likely to occur. |





&nbsp;
## Application Usage

Once installed, access the Web UI at `<your-ip>` from a web browser.

Alternatively, you can use the [Glimmr Mobile app](https://github.com/d8ahazard/GlimmrMobile/releases/tag/1.0) (Android, UWP).

### Discover Devices
Discovery should be auto-triggered when the application runs for the first time.

If devices are missing or you want to re-scan, the refresh button is in the lower-right corner
of the web UI.

### Configure Glimmr Settings
To configure system, capture, and audio preferences, click the gear icon in the top-right corner
of the screen.

### Configure OpenRGB
[OpenRGB](https://gitlab.com/CalcProgrammer1/OpenRGB) is a free, cross-platform solution for controlling desktop-connected lighting peripherals.

Once installed, ensure OpenRGB is running and the SDK server is started. Refer to the [OpenRGB FAQ](https://gitlab.com/CalcProgrammer1/OpenRGB/-/wikis/Frequently-Asked-Questions)
for information regarding setting it up as a service.

Under Settings -> General -> OpenRGB, enter the IP Address of the computer on which OpenRGB is running.

Now, trigger a device refresh, and any devices in OpenRGB should be added to the Glimmr UI.

### Configure Adalight Devices
NOTE: It is *highly* recommended to use my [custom Adalight Sketch](https://github.com/d8ahazard/adalight-FastLED-Plus), as it provides
additional features and controls that help better integrate with Glimmr, including auto-detection
of brightness and LED count, adjusting brightness during runtime, and gamma correction.

If you're not using my sketch, you will need to specify your strip's LED count.

You can also specify an offset (number of pixels to skip before displaying LED colors), baud rate,
and whether or not to reverse the order of the colors sent to your device.

### Configure your Lifx Bulbs
Select your discovered bulb in the Web UI. Click the desired sector to map your bulb to that sector. Repeat for all bulbs.

### Configure your Lifx Beams
Select your discovered beam in the Web UI. A layout will be auto-generated based on the number of "pixels" your beam has.

However, Beams cannot specify the order segments are in (where corners are located), so you will need to manually
order these in the Web UI. Drag-and-drop to re-order. Numbers are arbitrary, and only there to help understand
what you're looking at.

For beams, the internal LEDs are spaced so that 1 beam pixel occurs roughly ever 2 pixels in a 60 LED/M strip.

Each segment has several options:
Offset: Where to start from around the frame.
Repeat: Should the color at the offset pixel be repeated for the entire segment.
Mirror: Reverse the direction of colors sent to the segment.


### Configure Your Nanoleaf Panels
Select your Nano device in the web UI. Click the "Click here to link..." button. Press and hold the power button on your nanoleaf panel until the Web UI shows "linked".

Once linked, your panel layout will be shown in the Web UI (I suck, so you may need to reload the page for now). Drag it around to set the position in relation to the screen, and your lights will be auto-mapped to the proper sectors.

Click each panel in the UI to open the sector selection UI. Click a sector to map that panel to it.

### Configure Your Hue Bridge
Select your Hue Bridge in the web UI. Click the "Click here to link..." button. Within 30 seconds, press the link button on your Hue Bridge. The Web UI should update to show that the bridge has been linked - if not, reload the page.

#### Create A Hue Entertainment Group!! (NEW)
Go into your hue app. In the side menu, select "Entertainment areas".

Create a new entertainment area and configure it with your lights as they are in the room.

#### Configure Hue Light Mappings
Back in the glimmr web UI, reload the page, then select your hue bridge, and select the newly created entertainment group from the drop-down.
For each light you wish to map, select the sector in the dropdown that corresponds to the sector you wish to map to.

If no entertainment groups are showing and you're linked already, use the refresh button to re-scan entertainment
groups and light settings.

#### Configure WLED
NOTE: There is a "bug" with the WLED software that causes discovery to fail under some
circumstances. If Glimmr does not find your WLED devices, reboot them, and then under
WLED settings -> "Security and Updates", scroll down and UNCHECK "Enable ArduinoOTA".
This will fix discovery for your WLED devices.

WLED devices have several different options and display modes.

"Normal" mode matches up the WLED strip to the perimeter of the screen using the LED counts
specified in the Glimmr capture settings. You can specify an offset, which will skip that many
LEDs from the "total" around the screen. You can also specify to reverse the direction of the colors
if the strip is positioned opposite of the screen.

"Loop (Play Bar)" mode is similar to "Normal", but assumes that the LEDs are in some sort of U-shape,
where there are two pixels for a strip that correspond to a segment of the screen.

"Sectored" currently does nothing, as I haven't implemented it yet.

"Single Color" assumes that you have a small number of LEDs in a close area, and you want all of
those LEDs to display the same color. The color being displayed will be whatever the strip's offset
is set to.

Click the "Save settings" button to submit your configuration.


## Integrations

### Swagger API

Glimmr has a fully REST-ful API that supports control and enumeration of nearly every method and
relevant data structure in the application. All methods are documented on (https://app.swaggerhub.com/apis/d8ahazard/glimmr/)[Swagger Hub].
Additionally, you can access the API documentation locally by going to http://<YOUR_GLIMMR_ADDRESS>/swagger/index.html.

The local API reference can be used to view and set data in real-time, as well as see in-depth descriptions 
of what each device and system setting do. You can also examine the data structures of Ambient and Audio Scenes.


### Python Library

Want to create a python project to control Glimmr? There's a package for that.
Presently, this is not a full implementation of the Swagger API, but only adds the features
required for implementation with Home Assistant. If a feature is not implemented, 
feel free to submit a feature request to the below github repo.

https://pypi.org/project/glimmr/
https://github.com/d8ahazard/glimmr-python


## Home Assistant Integration

Running a Home Assistant instance? Well, you're in luck. I've created a platinum-level
integration for Home Assistant that takes full advantage of the features provided by Glimmr.

The integration includes automatic discovery via MDNS/Zeroconfig, automatic push updates via websocket,
and the ability to change device modes and adjust ambient color and scenes.

Also, the integration is supported by HACS - you can just copy the below URL and add it as a custom 
repository, then install via the settings -> integrations page.

https://github.com/d8ahazard/glimmr_ha


## PROFIT

From here, you can use the app normally to control your devices. Open up the Dreamscreen mobile app, add new devices if your Glimmr device is not deteced, and then place it in a group with a DS device (If using DS sync mode).

To stop synchronization, select the device in the DS app or Glimmr Web UI and set the mode to "Off".

To start synchronization, select the GROUP in the DS app, (or device in Glimmr Web UI) and set the mode to "Video". If the device is not in a group, you can control it directly.


## UPDATING
Click the update button under settings-> general.
For docker...just recreate the container. :)

## NOTES

Ambient Scenes and music mode *are* now implemented. I still have some work to do with Mic detection, but the functionality exists.
Not all settings for DS devices in the Web UI are implemented...but the video and advanced settings should now be working properly.

Logs are stored to the following location(s):
Windows - %programdata%\Glimmr\log\
Linux - /var/log/Glimmr/

Glimmr storage DB and scenes are stored in the following location(s):
Windows - %programdata%\Glimmr\
Linux - /etc/Glimmr/

The application is installed to the following location(s):
Windows - C:\Program Files\Glimmr\
Linux - /usr/share/Glimmr/
OSX - /Applications/Glimmr/

## Related links:
[Home Assistant Integration](https://github.com/d8ahazard/Glimmr_ha)

[Python Library](https://github.com/d8ahazard/Glimmr-python)

[Raspberry pi image generator](https://github.com/d8ahazard/Glimmr-image-gen)

[Glimmr mobile app (Play Store)](https://play.google.com/store/apps/details?id=com.digitalhigh.GlimmrControl)

[FloW LED Android Screen Capture App (Play Store)](https://play.google.com/store/apps/details?id=com.digitalhigh.glimmrextender&hl=en_US&gl=US)

[Glimmr mobile app (source)](https://github.com/d8ahazard/GlimmrControl)

[DreamScreen Documents](https://github.com/d8ahazard/DreamscreenDocs/)


## THANKS!
Mad props to Greg F. for all the support, and Dr. Ackula for all the help and testing.

Much love to all of my other supporters/fans/stalkers.

## Buy me a beer

If you like my work and want to say thanks, I humbly accept donations via paypal at donate.to.digitalhigh@gmail.com
