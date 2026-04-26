#!/bin/bash -e

# Historically this stage moved /etc/ld.so.preload aside so that the
# 32-bit raspi-copies-and-fills shim wouldn't blow up on arm64 builders.
# raspi-copies-and-fills is no longer installed (see 01-packages), so the
# preload dance is unnecessary on Bookworm arm64. Kept as an explicit
# no-op for clarity / pi-gen stage symmetry.
exit 0
