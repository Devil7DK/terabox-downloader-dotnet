#!/bin/bash

# Stop on error
set -e

# Read if docker_host file exists
# If not, ask for hostname

if [ -f docker_host ]; then
    HOSTNAME=$(cat docker_host)
    echo "Using Docker Hostname: ${HOSTNAME}"
    echo "Delete docker_host file to change hostname"
else
    echo "Enter Docker Hostname:"
    read HOSTNAME
    echo "${HOSTNAME}" > docker_host
fi

IMAGE=terabox-downloader-dotnet

COMMIT_ID=$(git log --format="%H" -n 1)
DATE=$(date +%Y%m%d)

REPOSITORY="${HOSTNAME}/${IMAGE}"
TAG="${REPOSITORY}:latest"

docker build . -t "${TAG}"
docker push "${TAG}"

# Read if ssh_host file exists
# If not, ask for hostname

if [ -f ssh_host ]; then
    SSH_HOSTNAME=$(cat ssh_host)
    echo "Using SSH Hostname: ${SSH_HOSTNAME}"
    echo "Delete ssh_host file to change hostname"
else
    echo "Enter Hostname (With user if needed):"
    read SSH_HOSTNAME
    echo "${SSH_HOSTNAME}" > ssh_host
fi

# Execute "./restart.sh terabox-downloader" on remote host from "/srv/docker" directory
ssh "${SSH_HOSTNAME}" "cd /srv/docker && ./restart.sh ${IMAGE}"
