#!/bin/bash
cd ../src/Glimmr
dpkg-buildpackage -b --no-sign
cd ../../script