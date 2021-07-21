#!/bin/bash

df -a | grep pi-gen/work | awk '{print $6}' | sort -r | xargs umount

