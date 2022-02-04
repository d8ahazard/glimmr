#!/bin/bash

for mnt in `df -a | grep pi-gen/work | awk '{print $6}' | sort -r`; do umount $mnt ; done

