# [d8ahazard/glimmr](https://github.com/d8ahazard/glimmr)

## Installation

### Windows
Create a directory where you want Glimmr to reside.

Download this script, save it into the directory you created, and run it.
https://raw.githubusercontent.com/d8ahazard/glimmr/master/setup_win.bat


### Raspberry Pi
Execute the following command:
```
bash <(curl -s https://raw.githubusercontent.com/d8ahazard/glimmr/master/setup_pi.sh)
```
You may want to reboot your computer after exectution if it's a first-time install...

*Alternatively*, you can flash a custom image directly to your pi from here:

https://mega.nz/folder/brR31IKS#B7EI5KTr24ZwXdpdeG6pTw 

You will need to use "BalenaEtcher", a free software for flashing the image.

https://www.balena.io/etcher/

Once Balena is installed and loaded, select the image file you downloaded from above, and make sure your 
micro SD card is connected to your computer. Select the SD card, and click "Flash" to begin.

Once the image is flashed, there are two more things you want to do.

The computer should have recognized the boot partition of the SD card and loaded it, most likely as 
drive D:. Find and open this folder.

Once opened, create two files.

The first is just an empty text file called "ssh". This is not required, but when created, will
enable you to remotely connect to your pi via Putty or other SSH client. If you don't know what this
means or have no use for it, skip this.

Secondly, create a file called "wpa_supplicant.conf". This is the configuration for your wireless network, 
and is probably something you'll want to do.

Open this file in notepad++ or another text editor, and paste the below text:

```
ctrl_interface=DIR=/var/run/wpa_supplicant GROUP=netdev
update_config=1
country=<Insert 2 letter ISO 3166-1 country code here>

network={
 ssid="<Name of your wireless LAN>"
 psk="<Password for your wireless LAN>"
}
```

Once pasted, edit the file accordingly. Replace <Inset 2 letter ISO...> with US, UK, whatever your country code is.

Replace <Name of your wireless LAN> with your network name, keeping the quotes.
Replace <Password for your wireless LAN> with your network password, again, keeping the quotes.

Save the file, and you're ready to go! Insert the SD card in your pi, power it on, and enjoy!


### Linux
Execute the following command:
```
bash <(curl -s https://raw.githubusercontent.com/d8ahazard/glimmr/master/setup_linux.sh)
```
You may want to reboot your computer after exectution if it's a first-time install...


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

## Parameters

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

Once installed, access the Web UI at `<your-ip>:5699`.

### Discover Devices
Discovery should be auto-triggered when you open the web UI. If devices are missing, you can open the side menu and then click the + icon to trigger a rescan. Discovered devices will automatically be added to the side menu.

### Configure Glimmr Settings
Open the side menu, and click the gear icon to access settings.
Optional capture modes are "DreamScreen (Default)", "Camera", "HDMI", and "Screen Capture".

Screen capture and HDMI are either WIP or not implemented.

If using the default Dreamscreen mode and you have more than one DS device, you can select the "target" dreamscreen to use color data from here. LED counts here should not be edited, as they are auto-populated. I should really just make sure that's disabled if this is selected.

If using a camera, it is assumed that you are using a raspberry Pi to power your LED strips. You will need to manually input the proper number of vertical and horizontal LED's in order for everything to work correctly. You can also select the camera type to use - either Raspberry Pi camera or Webcam. I need to implement auto enumeration of USB devices for easier camera selection, for now, it just assumes input cam device 1 is the target.

### Configure your Lifx Bulbs
Select your discovered bulb in the Web UI. Click the desired sector to map your bulb to that sector. Repeat for all bulbs.

### Configure Your Nanoleaf Panels
Select your Nano device in the web UI. Click the "Click here to link..." button. Press and hold the power button on your nanoleaf panel until the Web UI shows "linked".

Once linked, your panel layout will be shown in the Web UI. Drag it around to set the position in relation to the screen, and your lights will be auto-mapped to the proper sectors.


### Configure Your Hue Bridge
Select your Hue Bridge in the web UI. Click the "Click here to link..." button. Within 30 seconds, press the link button on your Hue Bridge. The Web UI should update to show that the bridge has been linked.

#### Create A Hue Entertainment Group!! (NEW)
Go into your hue app. In the side menu, select "Entertainment areas".

Create a new entertainment area and configure it with your lights as they are in the room.

#### Configure Hue Light Mappings
Back in the glimmr web UI, reload the page, then select your hue bridge, and select the newly created entertainment group from the drop-down.
For each light you wish to map, select the sector in the dropdown that corresponds to the sector you wish to map to.

Click the "Save settings" button to submit your configuration.

## PROFIT

From here, you can use the app normally to control your devices. Open up the DreamScreen mobile app, add new devices if your Glimmr device is not deteced, and then place it in a group with a DS device (If using DS sync mode).

To stop synchronization, select the device in the DS app or Glimmr Web UI and set the mode to "Off".

To start synchronization, select the GROUP in the DS app, (or device in Glimmr Web UI) and set the mode to "Video". If the device is not in a group, you can control it directly.


## UPDATING
For windows/linux/raspi, just re-run the setup script you executed to install Glimmr, and it will automatically download the latest source from github, stop services, compile the code from source, copy into place, and restart services. 

For docker...just recreate the container. :)

## NOTES

Ambient Scenes and music mode *are* now implemented. I still have some work to do with Mic detection, but the functionality exists.
Not all settings for DS devices in the Web UI are implemented...but the video and advanced settings should now be working properly.

## Buy me a beer

If you like my work and want to say thanks, I humbly accept donations via paypal at donate.to.digitalhigh@gmail.com
