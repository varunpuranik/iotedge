#!/bin/bash
#https://github.com/Azure/iotedge/blob/iiot/scripts/linux/cross-platform-rust-build.sh
###############################################################################
# This script builds a static binary of the api-proxy-module
###############################################################################

set -e

###############################################################################
# Define Environment Variables
###############################################################################
# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)

SCRIPT_NAME=$(basename "$0")
BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../..}
PROJECT_ROOT=${BUILD_REPOSITORY_LOCALPATH}

IMAGE=api-proxy-module:latest

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
function usage()
{
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo " -h,  --help                   Print this help and exit."
    echo " -i,  --image                  Image name"
    echo " -t,  --target-arch            Target architecture: amd64|arm32v7|aarch64"
    exit 1;
}

###############################################################################
# Function to obtain the underlying architecture and check if supported
###############################################################################
check_arch()
{
    if [[ "$ARCH" == "amd64" ]]; then
        ARCH="amd64"
    elif [[ "$ARCH" == "arm32v7" ]]; then
        ARCH="arm32v7"
    elif [[ "$ARCH" == "aarch64" ]]; then
        ARCH="aarch64"
    else
        echo "Unsupported architecture"
        exit 1
    fi
}

###############################################################################
# Obtain and validate the options supported by this script
###############################################################################
process_args()
{
    save_next_arg=0
    for arg in "$@"
    do
        if [ $save_next_arg -eq 1 ]; then
            IMAGE="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 2 ]; then
            ARCH="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-i" | "--image" ) save_next_arg=1;;
                "-t" | "--target-arch" ) save_next_arg=2;;
                * ) usage;;
            esac
        fi
    done
}

process_args "$@"
check_arch


###############################################################################
# Build
###############################################################################

echo ${PROJECT_ROOT}

set +e
../../scripts/linux/cross-platform-rust-build.sh --os ubuntu18.04 --arch $ARCH --build-path edge-modules/api-proxy-module
set -e
strip ${PROJECT_ROOT}/edge-modules/api-proxy-module/target/x86_64-unknown-linux-musl/release/api-proxy-module

#if [ $ARCH == "amd64" ]; then
#	docker run --rm -it -v "${PROJECT_ROOT}":/home/rust/src ekidd/rust-musl-builder cargo build --release --manifest-path /home/rust/src/edge-modules/api-proxy-module/Cargo.toml
#	strip ${PROJECT_ROOT}/edge-modules/api-proxy-module/target/x86_64-unknown-linux-musl/release/api-proxy-module
#elif [ $ARCH == "arm32v7" ]; then
#	docker build -t api-proxy-module-builder ${PROJECT_ROOT}/edge-modules/api-proxy-module/docker/linux/builder
#  docker run --rm -it -v "${PROJECT_ROOT}":/home/rust/src api-proxy-module-builder cargo build --release --target=armv7-unknown-linux-gnueabihf --manifest-path /home/rust/src/edge-modules/api-proxy-module/Cargo.toml
#	docker run --rm -it -v "${PROJECT_ROOT}":/home/rust/src api-proxy-module-builder arm-linux-gnueabihf-strip /home/rust/src/edge-modules/api-proxy-module/target/armv7-unknown-linux-gnueabihf/release/api-proxy-module
#fi

docker build -t ${IMAGE} -f ${PROJECT_ROOT}/edge-modules/api-proxy-module/docker/linux/$ARCH/Dockerfile ${PROJECT_ROOT}/edge-modules/api-proxy-module/