# How It Works

Technical deep-dive into the rendering algorithm and performance optimizations.

**See also:** [README](../README.md) | [Braille Rendering](BRAILLE-RENDERING.md) | [Library API](LIBRARY-API.md)

---

## The Shape-Matching Algorithm

This library implements [Alex Harri's shape-matching approach](https://alexharri.com/blog/ascii-rendering) for converting images to ASCII art. Rather than mapping brightness to characters (the naive approach), it matches the **visual shape** of each image region to the closest-looking character.

### 1. Character Analysis

Each ASCII character is rendered and analyzed using **6 sampling circles in a 3x2 staggered grid**:

```
[0]  [1]  [2]   <- Top row (staggered vertically)
[3]  [4]  [5]   <- Bottom row
```

Left circles are lowered, right circles are raised to minimize gaps while avoiding overlap. Each circle samples "ink coverage" to create a 6D shape vector.

### 2. Normalization

All character vectors are normalized by dividing by the maximum component value across ALL characters, ensuring comparable magnitudes.

### 3. Image Sampling

Input images are sampled the same way, creating shape vectors for each output cell.

### 4. Contrast Enhancement

**Global contrast** (power function):

```
value = (value / max)^power * max
```

This "crunches" lower values toward zero while preserving lighter areas.

**Directional contrast** (10 external circles): External sampling circles reach outside each cell. For each internal component:

```
maxValue = max(internal, external)
result = applyContrast(maxValue)
```

This enhances edges where content meets empty space.

### 5. K-D Tree Matching

Fast nearest-neighbor search in 6D space finds the character whose shape vector best matches each image cell. Results are cached for repeated lookups.

### 6. Animation Optimization

For GIFs, frame differencing computes only changed pixels between frames, using ANSI cursor positioning to update efficiently.

---

## Performance

### Computation

- **SIMD optimized**: Uses `Vector128`/`Vector256`/`Vector512` for distance calculations
- **Parallel processing**: Multi-threaded rendering for ASCII, ColorBlock, and Braille modes
- **Pre-computed trigonometry**: Circle sampling uses lookup tables (eliminates ~216 trig calls per cell)
- **Caching**: Quantized vector lookups cached with 5-bit precision
- **Optimized bounds checking**: Branchless `(uint)x < (uint)width` pattern

### Animation Smoothness

Multiple techniques ensure flicker-free animation:

- **DECSET 2026 Synchronized Output**: Batches frame output for atomic rendering (supported by Windows Terminal, WezTerm, Ghostty, Alacritty, iTerm2)
- **Diff-based rendering**: Only updates changed lines between frames - no per-line clearing that causes black flashes
- **Overwrite with padding**: Lines are overwritten in place with space padding, eliminating flicker completely
- **Dynamic resize**: Animations automatically re-render when you resize the console window
- **Smooth loop transitions**: Automatic interpolation between last and first frames creates seamless loops
- **Cursor hiding**: `\x1b[?25l` hides cursor during playback
- **Pre-buffering**: All frames, diffs, and transitions converted to strings before playback
- **Immediate flush**: `Console.Out.Flush()` after each frame
