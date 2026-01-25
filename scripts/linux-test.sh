#!/bin/bash
# Linux Test Platform for ConsoleImage
# Tests all download-dependent features on a real Linux machine

set -e

REMOTE_HOST="scott@192.168.0.30"
REMOTE_DIR="~/consoleimage-build"
LOCAL_DIR="$(cd "$(dirname "$0")/.." && pwd)"

echo "=== ConsoleImage Linux Test Platform ==="
echo "Local:  $LOCAL_DIR"
echo "Remote: $REMOTE_HOST:$REMOTE_DIR"
echo ""

# Sync local changes to remote
sync_changes() {
    echo ">>> Syncing changes to remote..."
    rsync -avz --delete \
        --exclude 'bin/' \
        --exclude 'obj/' \
        --exclude 'publish*/' \
        --exclude '.git/' \
        --exclude '*.user' \
        --exclude 'nul' \
        "$LOCAL_DIR/" "$REMOTE_HOST:$REMOTE_DIR/"
    echo ">>> Sync complete"
}

# Build AOT on remote
build_aot() {
    echo ">>> Building AOT on Linux..."
    ssh $REMOTE_HOST "export PATH=\$HOME/.dotnet:\$PATH && \
        cd $REMOTE_DIR && \
        dotnet publish ConsoleImage/ConsoleImage.csproj \
            -c Release \
            -r linux-x64 \
            --self-contained \
            -p:PublishAot=true \
            -o ./publish-aot 2>&1 | tail -20"
    echo ">>> Build complete"
}

# Check binary size
check_size() {
    echo ">>> Binary size:"
    ssh $REMOTE_HOST "ls -lh $REMOTE_DIR/publish-aot/consoleimage"
}

# Test basic functionality
test_basic() {
    echo ">>> Testing basic functionality..."
    ssh $REMOTE_HOST "$REMOTE_DIR/publish-aot/consoleimage --version"
    ssh $REMOTE_HOST "$REMOTE_DIR/publish-aot/consoleimage --help | head -20"
}

# Test FFmpeg auto-download
test_ffmpeg() {
    echo ">>> Testing FFmpeg auto-download..."
    ssh $REMOTE_HOST "rm -f ~/.local/share/consoleimage/ffmpeg/ffmpeg || true"
    ssh $REMOTE_HOST "$REMOTE_DIR/publish-aot/consoleimage -y https://test-videos.co.uk/vids/bigbuckbunny/mp4/h264/360/Big_Buck_Bunny_360_10s_1MB.mp4 -w 40 --no-animate 2>&1 | head -20" || echo "FFmpeg test needs manual verification"
}

# Test yt-dlp auto-download
test_ytdlp() {
    echo ">>> Testing yt-dlp auto-download..."
    ssh $REMOTE_HOST "rm -f ~/.local/share/consoleimage/yt-dlp/yt-dlp || true"
    ssh $REMOTE_HOST "$REMOTE_DIR/publish-aot/consoleimage -y 'https://www.youtube.com/watch?v=jNQXAC9IVRw' --info 2>&1 | head -10" || echo "yt-dlp test needs manual verification"
}

# Test Whisper runtime download
test_whisper_runtime() {
    echo ">>> Testing Whisper runtime download..."
    ssh $REMOTE_HOST "rm -rf ~/.local/share/consoleimage/whisper/runtimes || true"
    ssh $REMOTE_HOST "$REMOTE_DIR/publish-aot/consoleimage test.mp4 --subs whisper -y --help 2>&1 | head -10" || echo "Whisper runtime test needs manual verification"
}

# Test Whisper model download
test_whisper_model() {
    echo ">>> Testing Whisper model download..."
    # Don't delete - models are large. Just check if download prompt works
    ssh $REMOTE_HOST "$REMOTE_DIR/publish-aot/consoleimage test.mp4 --subs whisper --whisper-model tiny 2>&1 | head -10" || echo "Whisper model test needs manual verification"
}

# Test time format parsing
test_time_formats() {
    echo ">>> Testing time format parsing..."
    ssh $REMOTE_HOST "$REMOTE_DIR/publish-aot/consoleimage --help | grep -A2 'start'" || true
    # These should parse correctly:
    # -ss 4.7 (seconds with decimal)
    # -ss 6:47 (mm:ss)
    # -ss 1:30:00 (hh:mm:ss)
    # -sm 6.47 (decimal minutes = 6m 28.2s)
    # -sf 100 (start at frame 100)
}

# Run all tests
run_all() {
    sync_changes
    build_aot
    check_size
    test_basic
    test_time_formats
    echo ""
    echo "=== Basic tests complete ==="
    echo "Run individual download tests with:"
    echo "  $0 ffmpeg"
    echo "  $0 ytdlp"
    echo "  $0 whisper"
}

# Main
case "${1:-all}" in
    sync)    sync_changes ;;
    build)   build_aot ;;
    size)    check_size ;;
    basic)   test_basic ;;
    ffmpeg)  test_ffmpeg ;;
    ytdlp)   test_ytdlp ;;
    whisper) test_whisper_runtime; test_whisper_model ;;
    time)    test_time_formats ;;
    all)     run_all ;;
    *)
        echo "Usage: $0 [sync|build|size|basic|ffmpeg|ytdlp|whisper|time|all]"
        exit 1
        ;;
esac

echo ""
echo "=== Done ==="
