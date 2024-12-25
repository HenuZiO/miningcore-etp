#!/bin/bash

# Update Homebrew and install dependencies
brew update
brew install wget git cmake openssl boost libsodium zmq

# Install dotnet-sdk
brew tap isen-ng/dotnet-sdk-versions
brew install --cask dotnet-sdk6-0-400

# Build the project
(cd src/Miningcore && \
BUILDIR=${1:-../../build} && \
echo "Building into $BUILDIR" && \
dotnet publish -c Release --framework net6.0 -o $BUILDIR)
