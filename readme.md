# [d8ahazard/glimmr](https://github.com/d8ahazard/glimmr)

## Installation

### Raspberry Pi
Execute the following command:
bash <(curl -s https://raw.githubusercontent.com/d8ahazard/glimmr/master/setup_pi.sh)

### Windows
Create a directory where you want Glimmr to reside.

Download this script, save it into the directory you created, and run it.
https://raw.githubusercontent.com/d8ahazard/glimmr/master/setup_win.bat


### Linux
For Linux, download the latest release and extract to /opt/glimmr (or wherever you want). I'll provide instructions on how to install as a service
as soon as I figure out how to do it. ;)

For Docker (recommended), see below...

### docker

Use the following command. You don't need to specify the ASPNETCORE_URLS value, unless you wish to change the default
port that the web UI listens on. If so, modify the port number. e.g.: 'http://+:5699' to 'http://+:5699'

```
docker create \
  --name=glimmr \
  -v <path to data>:/etc/glimmr \
  -p 1900:1900/udp \
  -p 2100:2100/udp \
  -p 5699:5699 \
  -p 8888:8888/udp \ 
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
      - 5699:5699
      - 8888:8888/udp
```

## Parameters

Container images are configured using parameters passed at runtime (such as those above). These parameters are separated by a colon and indicate `<external>:<internal>` respectively. For example, `-p 8080:80` would expose port `80` from inside the container to be accessible from the host's IP on port `8080` outside the container.

| Parameter | Function |
| :----: | --- |
| `-v <path_to_data>/etc/glimmr` | Change <path_to_data> to the location where to keep glimmr ini |
| `-p 1900:1900\udp` | Hue discovery port |
| `-p 2100:2100\udp` | Hue broadcast port |
| `-p 5699:5699` | Web UI port |
| `-p 8888:8888\udp` | DS Emulation port |




&nbsp;
## Application Usage

Once installed, access the Web UI at `<your-ip>:5699`.

### Find Glimmr
In the Glimmr section, click the "Find Glimmr" button if your Glimmr ip is not shown in the UI. If you have more than one Glimmr, you're currently SOL until I write logic to handle that...

Optionally, you can select the type of DS device to emulate. If you choose "Connect", you wll need the beta version of the DS app.

### Link Your Hue Bridge
Press the link button on your hue bridge. Go to the Hue section, click "Authorize Hue". You should get a response stating your hue has been linked.

### Create An Entertainment Group!! (NEW)
Go into your hue app. In the side menu, select "Entertainment areas".

Create a new entertainment area and configure it with your lights as they are in the room.

### Configure Light Mappings
Back in the glimmr web UI, go to the "sync" section, and select your entertainment group.
For each light you wish to map, select the sector in the dropdown that corresponds to the sector you wish to map to.

Click the "Save settings" button to submit your configuration.

## PROFIT

Open up your Glimmr app. Select "Add new", and your device should show up as a sidekick or connect. 

From here, you can use the app normally to control your hue lights. You can rename it, add it to a group, and change the modes.

To stop synchronization, select the device in the DS app and set the mode to "Off".

To start synchronization, select the GROUP in the DS app, and set the mode to "Video". If the device is not in a group, control it directly.

## NOTES

Ambient scenes are not currently implemented, simply because this program is native to each DS device, and is not streamed.

Meaning, I will have to manually program each scene before this will work.


Saturation is not yet implemented. 


Sector mappings within the DS app currently do nothing...


## Buy me a beer

If you like my work and want to say thanks, I humbly accept donations via paypal at donate.to.digitalhigh@gmail.com
