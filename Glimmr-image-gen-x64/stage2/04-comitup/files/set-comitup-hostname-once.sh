#!/bin/bash
# set-comitup-hostname-once.sh
#
# Rename the system hostname to match the Comitup AP name once, then
# disarm itself so subsequent boots leave the (potentially user-edited)
# hostname alone.

set -e

STAMP=/var/lib/glimmr/hostname-set
mkdir -p "$(dirname "${STAMP}")"

if [ -f "${STAMP}" ]; then
    exit 0
fi

if ! command -v comitup >/dev/null 2>&1; then
    echo "set-comitup-hostname-once: comitup not installed, nothing to do"
    touch "${STAMP}"
    exit 0
fi

APNAME="$(comitup -i 2>/dev/null | awk '/apname:/ {print $2; exit}')"
if [ -z "${APNAME}" ]; then
    echo "set-comitup-hostname-once: no AP name yet, will retry next boot"
    exit 0
fi

echo "set-comitup-hostname-once: setting hostname to ${APNAME}"
hostnamectl set-hostname "${APNAME}"

# Keep /etc/hosts coherent so sudo and friends don't complain about the
# new hostname not resolving.
if grep -qE '^127\.0\.1\.1\b' /etc/hosts; then
    sed -i -E "s/^127\.0\.1\.1\b.*/127.0.1.1\t${APNAME}/" /etc/hosts
else
    printf '127.0.1.1\t%s\n' "${APNAME}" >> /etc/hosts
fi

touch "${STAMP}"
