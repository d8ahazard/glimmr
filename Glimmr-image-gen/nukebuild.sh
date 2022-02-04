#!/bin/bash

while `/bin/true`; do \
    ./cleanup; \
    rm -rf work/Comitup; \
    ./build.sh && exit 0; \
done
