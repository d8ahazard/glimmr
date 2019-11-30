# [d8ahazard/huedream](https://github.com/d8ahazard/huedream)

## Installation

### Windows
For Windows, download the latest release and extract wherever you like. You may need to unblock HueDream.exe from Windows Firewall.

To run as a service, try out [NSSM - The Non Sucking Service Manager](http://nssm.cc/)

### Linux
For Linux, download the latest release and extract to /opt/huedream (or wherever you want). I'll provide instructions on how to install as a service
as soon as I figure out how to do it. ;)

For Docker (recommended), see below...

### docker

Use the following command. You don't need to specify the ASPNETCORE_URLS value, unless you wish to change the default
port that the web UI listens on. If so, modify the port number. e.g.: 'http://+:5666' to 'http://+:5000'

```
docker create \
  --name=huedream \
  -e ASPNETCORE_URLS=http://+:5666 \
  -v <path to data>:/etc/huedream \ 
  --restart unless-stopped \
  --network=host \
  digitalhigh/huedream
```


### docker-compose

Compatible with docker-compose v2 schemas.

```
---
version: "2"
services:
  huedream:
    image: d8ahazard/huedream
    container_name: huedream
    environment:
      - ASPNETCORE_URLS=http://+:5666
    network_mode: "host"
    restart: unless-stopped
	volumes:
      - <path to data>:/etc/huedream
```

## Parameters

Container images are configured using parameters passed at runtime (such as those above). These parameters are separated by a colon and indicate `<external>:<internal>` respectively. For example, `-p 8080:80` would expose port `80` from inside the container to be accessible from the host's IP on port `8080` outside the container.

| Parameter | Function |
| :----: | --- |
| `-e ASPNETCORE_URLS=http://+:5666` | Modify port number as needed |
| `-v <path_to_data>/etc/huedream` | Change <path_to_data> to the location where to keep HueDream ini |




&nbsp;
## Application Usage

Once installed, access the Web UI at `<your-ip>:5666`.

### Find DreamScreen
In the DreamScreen section, click the "Find DreamScreen" button if your DreamScreen ip is not shown in the UI. If you have more than one DreamScreen, you're currently SOL until I write logic to handle that...

Optionally, you can select the type of DS device to emulate. If you choose "Connect", you wll need the beta version of the DS app.

### Link Your Hue Bridge
Press the link button on your hue bridge. Go to the Hue section, click "Authorize Hue". You should get a response stating your hue has been linked.

### Create An Entertainment Group!! (NEW)
Go into your hue app. In the side menu, select "Entertainment areas".

Create a new entertainment area and configure it with your lights as they are in the room.

### Configure Light Mappings
Back in the HueDream web UI, go to the "sync" section, and select your entertainment group.
For each light you wish to map, select the sector in the dropdown that corresponds to the sector you wish to map to.

Click the "Save settings" button to submit your configuration.

## PROFIT

Open up your DreamScreen app. Select "Add new", and your device should show up as a sidekick or connect. 

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
