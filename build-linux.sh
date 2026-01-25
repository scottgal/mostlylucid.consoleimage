#!/bin/bash
# Build ConsoleImage AOT for Linux
# Run this on the target Linux machine

set -e

echo "Building ConsoleImage for Linux..."

# Ensure .NET 10 SDK is installed
if ! command -v dotnet &> /dev/null; then
    echo "Installing .NET 10 SDK..."
    wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
    chmod +x dotnet-install.sh
    ./dotnet-install.sh --channel 10.0
    export PATH="$HOME/.dotnet:$PATH"
fi

# Build AOT
cd "$(dirname "$0")"
dotnet publish ConsoleImage/ConsoleImage.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained \
    -p:PublishAot=true \
    -o ./publish-linux-x64

# Show result
ls -lh ./publish-linux-x64/consoleimage
echo ""
echo "Binary ready at: ./publish-linux-x64/consoleimage"
echo "Test with: ./publish-linux-x64/consoleimage --help"
