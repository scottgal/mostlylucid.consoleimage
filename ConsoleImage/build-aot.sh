#!/bin/bash
# AOT Build Script for ConsoleImage (Linux/macOS)
# Run this script on a Linux or macOS machine to build native AOT

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "Building ConsoleImage with AOT for the current platform..."

# Detect platform
if [[ "$OSTYPE" == "linux-gnu"* ]]; then
    RID="linux-x64"
elif [[ "$OSTYPE" == "darwin"* ]]; then
    # Check for ARM64 vs x64
    if [[ "$(uname -m)" == "arm64" ]]; then
        RID="osx-arm64"
    else
        RID="osx-x64"
    fi
else
    echo "Unsupported platform: $OSTYPE"
    exit 1
fi

echo "Target RID: $RID"

# Build
dotnet publish "$SCRIPT_DIR/ConsoleImage.csproj" \
    -c Release \
    -r "$RID" \
    --self-contained \
    -p:PublishAot=true

echo "Build complete!"
echo "Output: $SCRIPT_DIR/bin/Release/net10.0/$RID/publish/"
