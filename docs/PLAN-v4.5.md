# v4.5 Release Plan — Perceptual Enhancement & Quality

## Vision

v4.5 introduces **perceptual braille interlacing** — a technique that plays multiple slightly different braille frames rapidly to create the illusion of enhanced detail beyond what a single frame can show. This is temporal dithering applied to terminal art.

Also includes color system improvements, Whisper transcription tuning, and a final quality pass.

---

## Part 1: Perceptual Braille Interlacing (Still Images)

**Concept:** A single braille character is a 2x4 grid of dots. Each dot is either on or off, determined by a brightness threshold. By varying the threshold across multiple rapid frames, we create temporal dithering — the viewer perceives more detail than any single frame contains.

**How it works:**
1. Render the source image N times with slightly different brightness thresholds
2. Frame 1: threshold = T - offset
3. Frame 2: threshold = T (baseline)
4. Frame 3: threshold = T + offset
5. Play these frames at high speed (e.g., 15-30fps)
6. The eye integrates the rapidly alternating dot patterns into perceived higher resolution

**Parameters:**
- `--interlace` or `--enhance` — Enable perceptual interlacing
- `--interlace-frames N` — Number of threshold-variant frames (default: 3)
- `--interlace-spread F` — Threshold variation range (default: 0.1)
- `--interlace-fps N` — Playback speed for interlaced frames (default: 20)

**Files to modify:**
| File | Changes |
|------|---------|
| `ConsoleImage.Core/BrailleRenderer.cs` | Add multi-threshold rendering method |
| `ConsoleImage.Core/BrailleInterlacePlayer.cs` | New: plays interlaced frames at high speed |
| `ConsoleImage/CliOptions.cs` | `--interlace`, `--interlace-frames`, `--interlace-spread` |
| `ConsoleImage/Program.cs` | Route to interlace player for still images |

**Implementation approach:**
- `BrailleRenderer` already has a threshold in its dot rendering. Add a `thresholdOffset` parameter
- Generate N frames with `threshold ± (i * spread / N)` for i in [0..N-1]
- Use existing `AsciiAnimationPlayer` for rapid playback
- For GIF output, embed all interlace frames at the target FPS

---

## Part 2: Inter-Frame Video Enhancement

**Concept:** Extend interlacing to video — for each source video frame, generate 2-3 braille variants and interleave them. This multiplies the effective refresh rate and perceived detail.

**How it works:**
1. For each video frame at time T:
   - Render braille with threshold T-offset, T, T+offset
   - Play all variants before advancing to the next source frame
2. Source at 10fps with 3 interlace frames = 30fps output

**Constraint:** Must not increase CPU load proportionally — use cached threshold maps and incremental updates.

**Files to modify:**
| File | Changes |
|------|---------|
| `ConsoleImage.Video.Core/VideoAnimationPlayer.cs` | Add interlace frame generation in render loop |
| `ConsoleImage.Core/BrailleRenderer.cs` | Support batch multi-threshold render (one image, N outputs) |

---

## Part 3: Color System Improvements

### 3a: `--colors` for Console Display

Currently `--colors N` only affects GIF palette. Extend it to also control ANSI color depth for terminal output:
- `--colors 16` → Use standard 16-color ANSI codes (most compatible)
- `--colors 256` → Use 256-color palette (xterm-256color)
- `--colors true` or no flag → Use 24-bit truecolor (default, current behavior)

**Files to modify:**
| File | Changes |
|------|---------|
| `ConsoleImage.Core/RenderOptions.cs` | Add `ColorDepth` enum (TrueColor, Palette256, Palette16) |
| `ConsoleImage.Core/AnsiCodes.cs` | Support 256-color and 16-color output modes |
| `ConsoleImage.Core/BrailleRenderer.cs` | Quantize colors to target depth |
| `ConsoleImage.Core/ColorBlockRenderer.cs` | Quantize colors to target depth |
| `ConsoleImage/CliOptions.cs` | Map `--colors` to `ColorDepth` |

### 3b: Greyscale vs Monochrome Semantics

Clarify the two modes:
- `--no-color` → **Greyscale**: Use brightness levels but no hue (ANSI grey ramp: 232-255)
- `--monochrome` / `--mono` → **Pure black & white**: Dots on/off, no grey levels

Currently `--mono` means "braille without color" which is already close to this, but `--no-color` disables all ANSI codes (outputs plain text). The improvement is to make `--no-color` output greyscale ANSI codes.

**Files to modify:**
| File | Changes |
|------|---------|
| `ConsoleImage.Core/BrailleRenderer.cs` | Add greyscale ANSI output path |
| `ConsoleImage.Core/ColorBlockRenderer.cs` | Add greyscale ANSI output path |
| `ConsoleImage.Core/RenderOptions.cs` | Add `GreyscaleMode` option |

---

## Part 4: Whisper Transcription Improvements

From the existing plan (already designed, implementation pending):

### FFmpeg Audio Preprocessing
Add speech-optimized filters before Whisper:
```
-af "highpass=f=200,lowpass=f=3000,afftdn=nf=-25,loudnorm"
```

### Whisper Processor Tuning
```csharp
.WithNoSpeechThreshold(0.6f)
.WithEntropyThreshold(2.4f)
.WithSuppressRegex(@"\[.*\]")
.WithBeamSearchSamplingStrategy()
```

### Energy-Based Silence Detection
Skip silent WAV chunks before Whisper processing (pure C#, no deps):
```csharp
private static bool HasSpeechEnergy(string wavPath, float rmsThreshold = 0.01f)
```

**Files to modify:**
| File | Changes |
|------|---------|
| `ConsoleImage.Transcription/ChunkedTranscriber.cs` | FFmpeg filters + silence detection + Whisper tuning |
| `ConsoleImage.Transcription/WhisperTranscriptionService.cs` | Processor builder tuning |

---

## Part 5: Final Quality Pass

- **Memory efficiency:** Ensure all Image<Rgba32> objects are properly disposed, ArrayPool buffers returned
- **CPU efficiency:** Profile hot paths, verify parallel rendering works correctly
- **Error handling:** No unhandled exceptions from user input — all errors produce helpful messages
- **Cleanup:** Remove dead code, unused variables, commented-out sections
- **GIF quality:** Verify 256-color output looks correct across all modes
- **Hash verification:** Use `--hash` to verify color changes produce expected visual differences

---

## Files Summary

| File | Parts |
|------|-------|
| `ConsoleImage.Core/BrailleRenderer.cs` | 1, 2, 3a, 3b |
| `ConsoleImage.Core/BrailleInterlacePlayer.cs` | 1 (new) |
| `ConsoleImage.Core/RenderOptions.cs` | 3a, 3b |
| `ConsoleImage.Core/AnsiCodes.cs` | 3a |
| `ConsoleImage.Core/ColorBlockRenderer.cs` | 3a, 3b |
| `ConsoleImage.Video.Core/VideoAnimationPlayer.cs` | 2 |
| `ConsoleImage.Transcription/ChunkedTranscriber.cs` | 4 |
| `ConsoleImage.Transcription/WhisperTranscriptionService.cs` | 4 |
| `ConsoleImage/CliOptions.cs` | 1, 3a |
| `ConsoleImage/Program.cs` | 1, 3a |

---

## Verification

1. `dotnet build` — Zero errors, zero new warnings
2. `consoleimage photo.jpg --interlace` — Verify visual enhancement on still
3. `consoleimage photo.jpg --hash` vs `--interlace --hash` — Compare hashes
4. `consoleimage video.mp4 --interlace` — Verify video interlacing works
5. `consoleimage photo.jpg --colors 16` — Verify 16-color ANSI output
6. `consoleimage photo.jpg --no-color` — Verify greyscale output
7. `consoleimage video.mp4 --subs whisper` — Verify improved transcription
8. Regenerate all samples with new features
9. Memory profiling pass with `dotnet-counters`
