#!/bin/bash
# AOT Build Script for ConsoleImage (Linux/macOS)
# Creates native AOT-compiled binary for the current platform

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "=== ConsoleImage AOT Build ==="
echo ""

# Detect platform and architecture
ARCH=$(uname -m)
OS=$(uname -s)

case "$OS" in
    Linux)
        case "$ARCH" in
            x86_64) RID="linux-x64" ;;
            aarch64) RID="linux-arm64" ;;
            *) echo "Unsupported architecture: $ARCH"; exit 1 ;;
        esac
        # Check AOT prerequisites
        if ! command -v clang &> /dev/null; then
            echo "Installing AOT prerequisites (clang, zlib1g-dev)..."
            if command -v apt-get &> /dev/null; then
                sudo apt-get update && sudo apt-get install -y clang zlib1g-dev
            elif command -v dnf &> /dev/null; then
                sudo dnf install -y clang zlib-devel
            else
                echo "Warning: Could not install prerequisites. Ensure clang and zlib are installed."
            fi
        fi
        ;;
    Darwin)
        case "$ARCH" in
            x86_64) RID="osx-x64" ;;
            arm64) RID="osx-arm64" ;;
            *) echo "Unsupported architecture: $ARCH"; exit 1 ;;
        esac
        ;;
    *)
        echo "Unsupported OS: $OS"
        exit 1
        ;;
esac

OUTPUT_DIR="$SCRIPT_DIR/bin/Release/net10.0/$RID/publish"

echo "Platform: $OS $ARCH"
echo "Target RID: $RID"
echo "Output: $OUTPUT_DIR"
echo ""

# Build with all AOT optimizations
dotnet publish "$SCRIPT_DIR/ConsoleImage.csproj" \
    -c Release \
    -r "$RID" \
    --self-contained true \
    -p:PublishAot=true \
    -p:OptimizationPreference=Size \
    -p:IlcOptimizationPreference=Size \
    -p:StripSymbols=true \
    -p:IlcGenerateStackTraceData=false

# Make executable
chmod +x "$OUTPUT_DIR/consoleimage"

echo ""
echo "=== Build complete! ==="
ls -lh "$OUTPUT_DIR/consoleimage"
echo ""
echo "Run with: $OUTPUT_DIR/consoleimage"
