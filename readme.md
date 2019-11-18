# [d8ahazard/docker-huedream](https://github.com/d8ahazard/docker-huedream)

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
  -v <path to data>:/opt/huedream \ 
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
      - <path to data>:/opt/huedream
```

## Parameters

Container images are configured using parameters passed at runtime (such as those above). These parameters are separated by a colon and indicate `<external>:<internal>` respectively. For example, `-p 8080:80` would expose port `80` from inside the container to be accessible from the host's IP on port `8080` outside the container.

| Parameter | Function |
| :----: | --- |
| `-e ASPNETCORE_URLS=http://+:5666` | Modify port number as needed |
| `-v <path_to_data>/opt/huedream` | Change <path_to_data> to the location where to keep HueDream ini |




&nbsp;
## Application Setup

Access the webui at `<your-ip>:5666`.

Click through each header, and ensure values are set for both the Hue Bridge and DreamScreen.