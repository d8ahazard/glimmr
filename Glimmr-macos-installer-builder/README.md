[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

# macOS Installer Builder (For Glimmr)
Generate macOS installers for your applications and products from one command.

## Dependencies

You will need [maven](https://maven.apache.org/install.html) and [ballerina](https://ballerina.io/downloads) installed before the script will work.



## Usage


Clone the repository:

```git clone https://github.com/d8ahazard/Glimmr-macos-installer-builder```

CD into the new directory, set permissions:

```
cd ./Glimmr-macos-installer-builder
chmod -R 777 ./*
```

Run the build script:
```
cd ./macOS-x64
./build-macos-x64.sh Glimmr 1.2.0
```

Skip signing for now...


Cheers!! 🍺
