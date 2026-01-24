# Slideshow Transitions Feature Plan

## Overview

Add smooth transition effects between slides in slideshow mode. Since we're working with ASCII/text-based rendering, transitions need to be designed specifically for terminal output.

## Proposed Transitions

### 1. **Fade** (Simple)
- Gradually dim the current slide (reduce brightness/contrast)
- Gradually brighten the next slide from black
- Implementation: Adjust ANSI color brightness values during transition frames

### 2. **Slide** (Horizontal/Vertical)
- Slide the current frame out while the next slides in
- Implementation: Shift character columns/rows with partial overlap
- Variants: `slide-left`, `slide-right`, `slide-up`, `slide-down`

### 3. **Wipe** (Directional)
- Progressive reveal of new slide over old
- Draw a "curtain" line that reveals the next image
- Variants: `wipe-left`, `wipe-right`, `wipe-up`, `wipe-down`, `wipe-diagonal`

### 4. **Star Wars Wipe** (Special)
- Classic Star Wars-style transition with a star/sparkle pattern at the wipe edge
- The wipe line has a starburst pattern using characters like `*`, `.`, `+`, `x`
- Optional: Brief "hyperspace" effect with streaming characters before wipe

### 5. **Dissolve** (Random Pixels)
- Randomly replace characters from old slide with new slide
- Characters "sparkle" in randomly
- Implementation: Track which cells have transitioned

### 6. **Crossfade** (Blend)
- Blend both images together during transition
- For ASCII: Alternate characters from both slides in a checkerboard that shifts
- For color modes: Actually blend colors if both rendered

### 7. **Zoom** (Scale)
- Current slide zooms out/shrinks to center
- Next slide zooms in from center
- Challenging in ASCII but possible with careful character scaling

### 8. **Matrix Rain** (Themed)
- Matrix-style falling characters reveal the next image
- Green cascading effect that "draws" the new slide

## Architecture

### New Files

```
ConsoleImage.Core/
  Transitions/
    ITransition.cs           # Interface for transitions
    TransitionBase.cs        # Base class with common utilities
    FadeTransition.cs
    SlideTransition.cs
    WipeTransition.cs
    StarWarsWipeTransition.cs
    DissolveTransition.cs
    CrossfadeTransition.cs
    MatrixRainTransition.cs
    TransitionFactory.cs     # Create transitions by name
```

### Interface Design

```csharp
public interface ITransition
{
    string Name { get; }

    /// <summary>
    /// Generate transition frames between two slides.
    /// </summary>
    /// <param name="fromFrame">The current/outgoing frame content</param>
    /// <param name="toFrame">The next/incoming frame content</param>
    /// <param name="width">Frame width in characters</param>
    /// <param name="height">Frame height in lines</param>
    /// <param name="durationMs">Total transition duration</param>
    /// <param name="frameIntervalMs">Time between frames (e.g., 33ms = 30fps)</param>
    /// <returns>Sequence of transition frames with delays</returns>
    IEnumerable<TransitionFrame> GenerateFrames(
        string fromFrame,
        string toFrame,
        int width,
        int height,
        int durationMs,
        int frameIntervalMs);
}

public record TransitionFrame(string Content, int DelayMs);
```

### CLI Options

```bash
# Enable transitions
consoleimage ./photos --transition fade

# Transition with duration
consoleimage ./photos --transition slide-left --transition-duration 500

# Star Wars mode!
consoleimage ./photos --transition starwars

# Random transitions
consoleimage ./photos --transition random

# List available transitions
consoleimage --list-transitions
```

### New CLI Options to Add

```csharp
public Option<string> Transition { get; }      // Transition name
public Option<int> TransitionDuration { get; } // Duration in ms (default: 300)
```

## Implementation Details

### Fade Transition
```
Frame 1: Original at 100% brightness
Frame 2: Original at 75% brightness
Frame 3: Original at 50% brightness
Frame 4: Original at 25% brightness
Frame 5: Black/empty
Frame 6: New at 25% brightness
Frame 7: New at 50% brightness
Frame 8: New at 75% brightness
Frame 9: New at 100% brightness
```

For ANSI colors, modify RGB values proportionally:
- `\x1b[38;2;R;G;Bm` -> `\x1b[38;2;R*factor;G*factor;B*factor;m`

### Slide Transition (Left)
```
Frame 1: [AAAAAAAAAA]
Frame 2: [AAAAAAAABB]  (A shifted left 2, B enters from right)
Frame 3: [AAAAAABBBB]
Frame 4: [AAAABBBBBB]
Frame 5: [AABBBBBBBB]
Frame 6: [BBBBBBBBBB]
```

### Star Wars Wipe
```
Frame 1: [AAAAAAAAAA]
Frame 2: [AAAAAAA*BB]  (* = sparkle edge)
Frame 3: [AAAA*.+*BBB]
Frame 4: [AA*+.x*BBBB]
Frame 5: [*+.x*BBBBBB]
Frame 6: [BBBBBBBBBB]
```

The wipe edge uses randomized sparkle characters: `*`, `.`, `+`, `x`, `o`
Optional "hyperspace" prelude with streaming `-` and `=` characters.

### Dissolve Transition
```
Frame 1: AAAAAAAAAA  (100% A)
Frame 2: AABAAAAAAA  (90% A, random B cells)
Frame 3: AABABAABAA  (80% A)
...
Frame 9: BBBBBBBBAB  (10% A)
Frame 10: BBBBBBBBBB (100% B)
```

## Memory Considerations

- Pre-render both frames before transition starts
- Generate transition frames on-demand (iterator pattern)
- Don't store all transition frames in memory at once
- Target 30fps for smooth transitions (33ms per frame)

## Background Pre-rendering (Silky Smooth Mode)

While displaying the current slide, pre-render upcoming slides in the background:

```csharp
// Pre-render cache
private ConcurrentDictionary<int, string> _preRenderedFrames = new();

// Background pre-rendering task
async Task PreRenderAheadAsync(int currentIndex, int lookahead = 2)
{
    for (int i = 1; i <= lookahead; i++)
    {
        var nextIndex = (currentIndex + i) % files.Count;
        if (!_preRenderedFrames.ContainsKey(nextIndex))
        {
            var rendered = await RenderFileAsync(files[nextIndex], options);
            _preRenderedFrames.TryAdd(nextIndex, rendered);
        }
    }
}
```

Benefits:
- Zero delay when advancing to next slide
- Transitions start immediately
- Especially important for large images or braille mode
- Also pre-render previous slide for backwards navigation

Cache management:
- Keep current, previous, and next 2 slides in cache
- Evict slides that are > 3 positions away
- Limit total cache size to prevent memory issues

## Terminal Compatibility

- Use synchronized output (DECSET 2026) to prevent flicker
- Alternate screen buffer for clean display
- Fall back gracefully if terminal doesn't support certain features
- Test on: Windows Terminal, iTerm2, GNOME Terminal, VS Code Terminal

## Phase 1 (MVP)
1. Add `ITransition` interface to Core
2. Implement `FadeTransition` (simplest)
3. Implement `WipeTransition` (classic)
4. Add CLI options
5. Integrate with SlideshowHandler

## Phase 2 (Enhanced)
1. Implement `SlideTransition` (all directions)
2. Implement `DissolveTransition`
3. Implement `StarWarsWipeTransition`
4. Add `--transition random` option

## Phase 3 (Advanced)
1. Implement `CrossfadeTransition`
2. Implement `MatrixRainTransition`
3. Implement `ZoomTransition`
4. Custom transition plugins?

## Testing

- Visual testing with sample images
- Performance testing (ensure 30fps minimum)
- Terminal compatibility matrix
- Edge cases: very small/large dimensions, monochrome mode

## Example Usage

```bash
# Basic slideshow with fade transitions
consoleimage ./vacation-photos --transition fade

# Fast slide transitions
consoleimage ./art --transition slide-left --transition-duration 200

# Epic Star Wars style presentation
consoleimage ./presentation --transition starwars --slide-delay 5

# Random transitions for variety
consoleimage ./album --transition random --transition-duration 400
```
