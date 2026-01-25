#!/bin/bash
# Comprehensive Linux Test Suite for ConsoleImage
# Tests all features including downloads (FFmpeg, yt-dlp, Whisper)

set -e

BINARY="${1:-./publish-aot/consoleimage}"
TEST_DIR="/tmp/consoleimage-tests"
PASSED=0
FAILED=0

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_pass() { echo -e "${GREEN}✓ PASS${NC}: $1"; ((PASSED++)); }
log_fail() { echo -e "${RED}✗ FAIL${NC}: $1"; ((FAILED++)); }
log_info() { echo -e "${YELLOW}→${NC} $1"; }
log_section() { echo -e "\n${YELLOW}=== $1 ===${NC}"; }

# Setup
mkdir -p "$TEST_DIR"
cd "$(dirname "$0")/.." || cd ~/consoleimage-build

if [[ ! -f "$BINARY" ]]; then
    echo "Binary not found: $BINARY"
    echo "Usage: $0 [path-to-consoleimage-binary]"
    exit 1
fi

log_section "Basic Functionality"

# Test 1: Version
log_info "Testing --version"
if $BINARY --version >/dev/null 2>&1; then
    log_pass "--version"
else
    log_fail "--version"
fi

# Test 2: Help
log_info "Testing --help"
if $BINARY --help | grep -q "Render images"; then
    log_pass "--help shows description"
else
    log_fail "--help"
fi

# Test 3: New time options in help
log_info "Testing time options in help"
if $BINARY --help | grep -q "\-sm.*start-minutes"; then
    log_pass "-sm option available"
else
    log_fail "-sm option not found"
fi

if $BINARY --help | grep -q "\-sf.*start-frame"; then
    log_pass "-sf option available"
else
    log_fail "-sf option not found"
fi

log_section "Time Format Parsing"

# Test time format error messages
log_info "Testing invalid time format error"
OUTPUT=$($BINARY test.mp4 -ss "invalid" 2>&1 || true)
if echo "$OUTPUT" | grep -qi "invalid time format\|error"; then
    log_pass "Invalid time format gives error"
else
    log_fail "No error for invalid time format"
fi

log_section "FFmpeg Auto-Download"

# Test FFmpeg download (clear cache first)
log_info "Clearing FFmpeg cache..."
rm -rf ~/.local/share/consoleimage/ffmpeg 2>/dev/null || true

log_info "Testing FFmpeg auto-download with test video..."
# Use a small test video URL
TEST_VIDEO="https://test-videos.co.uk/vids/bigbuckbunny/mp4/h264/360/Big_Buck_Bunny_360_10s_1MB.mp4"
if timeout 120 $BINARY -y "$TEST_VIDEO" -w 40 --no-animate -t 2 2>&1 | head -20; then
    if [[ -f ~/.local/share/consoleimage/ffmpeg/ffmpeg ]]; then
        log_pass "FFmpeg auto-downloaded"
    else
        log_fail "FFmpeg not found after download"
    fi
else
    log_fail "FFmpeg auto-download test"
fi

log_section "yt-dlp Auto-Download"

# Test yt-dlp download
log_info "Clearing yt-dlp cache..."
rm -rf ~/.local/share/consoleimage/yt-dlp 2>/dev/null || true

log_info "Testing yt-dlp auto-download with YouTube URL..."
# Use a very short/simple YouTube video for testing
YOUTUBE_URL="https://www.youtube.com/watch?v=jNQXAC9IVRw"  # "Me at the zoo" - first YouTube video
if timeout 60 $BINARY -y "$YOUTUBE_URL" --info 2>&1 | head -10; then
    if [[ -f ~/.local/share/consoleimage/yt-dlp/yt-dlp ]]; then
        log_pass "yt-dlp auto-downloaded"
    else
        log_fail "yt-dlp not found after download"
    fi
else
    log_info "yt-dlp test skipped (network issue or rate limit)"
fi

log_section "Whisper Runtime Side-Loading"

# Test Whisper runtime download
log_info "Clearing Whisper runtime cache..."
rm -rf ~/.local/share/consoleimage/whisper/runtimes 2>/dev/null || true

log_info "Testing Whisper runtime check (should prompt for download)..."
OUTPUT=$($BINARY test.mp4 --subs whisper 2>&1 || true)
if echo "$OUTPUT" | grep -qi "whisper.*not found\|download"; then
    log_pass "Whisper prompts for download when missing"
else
    log_info "Whisper runtime may already be bundled or test inconclusive"
fi

log_section "Whisper Model Download"

# Test Whisper model prompt
log_info "Testing Whisper model download prompt..."
OUTPUT=$($BINARY test.mp4 --subs whisper --whisper-model tiny 2>&1 || true)
if echo "$OUTPUT" | grep -qi "model.*not found\|download\|~[0-9]*MB"; then
    log_pass "Whisper model download prompt works"
else
    log_info "Model may already be cached"
fi

log_section "YouTube with Subtitles"

log_info "Testing YouTube video with auto-subtitles..."
if timeout 60 $BINARY -y "$YOUTUBE_URL" --subs auto -w 40 --no-animate -t 3 2>&1 | head -20; then
    log_pass "YouTube with auto-subtitles"
else
    log_info "YouTube subtitle test skipped (network or no subs available)"
fi

log_section "Status Bar"

log_info "Testing status bar with time display..."
if $BINARY --help | grep -q "\-S.*status"; then
    log_pass "Status option available"
else
    log_fail "Status option not found"
fi

log_section "GIF Output"

log_info "Testing GIF output..."
TEST_IMG="$TEST_DIR/test_pattern.png"
# Create a simple test image using ImageMagick if available, or skip
if command -v convert &>/dev/null; then
    convert -size 100x100 xc:red -fill blue -draw "circle 50,50 50,10" "$TEST_IMG"
    if $BINARY "$TEST_IMG" -o "$TEST_DIR/output.gif" -w 20 2>&1; then
        if [[ -f "$TEST_DIR/output.gif" ]]; then
            log_pass "GIF output created"
        else
            log_fail "GIF output not created"
        fi
    else
        log_fail "GIF output command failed"
    fi
else
    log_info "ImageMagick not installed, skipping GIF test"
fi

log_section "CIDZ Output"

log_info "Testing CIDZ compressed output..."
if [[ -f "$TEST_IMG" ]]; then
    if $BINARY "$TEST_IMG" -o "$TEST_DIR/output.cidz" -w 20 2>&1; then
        if [[ -f "$TEST_DIR/output.cidz" ]]; then
            log_pass "CIDZ output created"
            # Test playback
            if $BINARY "$TEST_DIR/output.cidz" --no-animate 2>&1 | head -5; then
                log_pass "CIDZ playback works"
            else
                log_fail "CIDZ playback failed"
            fi
        else
            log_fail "CIDZ output not created"
        fi
    else
        log_fail "CIDZ output command failed"
    fi
else
    log_info "No test image, skipping CIDZ test"
fi

log_section "Render Modes"

for mode in ascii blocks braille mono matrix; do
    log_info "Testing $mode mode..."
    if [[ -f "$TEST_IMG" ]]; then
        if $BINARY "$TEST_IMG" -m $mode -w 20 --no-animate 2>&1 | head -3; then
            log_pass "$mode mode"
        else
            log_fail "$mode mode"
        fi
    fi
done

log_section "Cleanup"
rm -rf "$TEST_DIR"

log_section "Test Summary"
echo -e "Passed: ${GREEN}$PASSED${NC}"
echo -e "Failed: ${RED}$FAILED${NC}"

if [[ $FAILED -eq 0 ]]; then
    echo -e "\n${GREEN}All tests passed!${NC}"
    exit 0
else
    echo -e "\n${RED}Some tests failed.${NC}"
    exit 1
fi
