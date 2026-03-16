# Dual-Color Braille Mode

**The best of both worlds: braille's 8x resolution combined with block mode's filled backgrounds.**

## Table of Contents

- [What Is Dual-Color Mode?](#what-is-dual-color-mode)
- [Quick Start](#quick-start)
- [How It Works](#how-it-works)
- [Color Strategies](#color-strategies)
- [Strategy Comparison](#strategy-comparison)
- [Technical Details](#technical-details)
- [Tips and Tricks](#tips-and-tricks)
- [Combining with Other Options](#combining-with-other-options)

---

## What Is Dual-Color Mode?

Standard braille rendering assigns a single foreground color to each character cell. The 8 dots within that cell share one color; the background defaults to the terminal's color. Dual-color mode assigns **independent colors to both the dot foreground and the cell background**, using the image's own pixel data for each.

```
Standard Braille:        Dual-Color Braille:

  ⣿⡇⢸⣿               ⣿⡇⢸⣿
  (FG only,            (FG = bright pixels,
   BG = terminal)       BG = dark pixels)
```

The result bridges the gap between braille's crisp dot-pattern detail and block mode's smooth filled appearance. Gradients look smoother, shadows have depth, and the overall image quality is noticeably richer.

---

## Quick Start

```bash
# Enable dual-color with default strategy
consoleimage photo.jpg --dual

# Short form
consoleimage photo.jpg -D

# With a specific color strategy
consoleimage photo.jpg --dual --dual-strategy complement
consoleimage photo.jpg -D --ds warmcool

# Video with dual-color
consoleimage movie.mp4 --dual

# Slideshow with dual-color
consoleimage ./photos --dual --ds saturate
```

---

## How It Works

### Standard Braille Pipeline

In standard braille mode, each 2×4 pixel region produces:
1. A braille character (which dots are ON) from Atkinson dithering
2. A single foreground color averaged from the lit dot pixels

The terminal background shows through wherever dots are OFF.

### Dual-Color Pipeline

Dual-color mode extends this with a second color for the background:

```
┌─────────────────────────────────────────────────────────┐
│  2×4 pixel region                                       │
│                                                         │
│  1. Compute braille character via Atkinson dithering    │
│                          ↓                              │
│  2. Split pixels by original brightness vs Otsu threshold│
│     · Bright pixels  → FG group → average → FG color   │
│     · Dark pixels    → BG group → average → BG color   │
│                          ↓                              │
│  3. Apply color strategy (value / complement / etc.)    │
│                          ↓                              │
│  4. Emit: ESC[38;2;R;G;Bm  (FG)                        │
│           ESC[48;2;R;G;Bm  (BG)                        │
│           <braille char>                                │
└─────────────────────────────────────────────────────────┘
```

### Why Original Brightness for Color Split?

A critical detail: the FG/BG split uses the **original (pre-dithered)** pixel brightness against the Otsu threshold — not the binary dithered values. This avoids dithering-pattern banding.

Atkinson dithering creates spatially structured patterns: error diffuses left-to-right and top-to-bottom, producing different ON/OFF distributions row-by-row. If those dithered binary values were used to determine color groups, adjacent braille character rows would get systematically different color mixes — producing horizontal banding artifacts. Using the original continuous brightness gives stable, content-driven splits.

### Otsu Threshold for Balanced Splits

Dual-color mode always uses **Otsu's method** to compute the FG/BG threshold. Otsu finds the threshold that maximizes the variance between two classes (bright and dark pixels), ensuring roughly equal representation of both FG and BG pixels per image. This means every cell has visible content in both the dots and the background fill — neither color dominates everywhere.

---

## Color Strategies

Control how FG and BG colors relate to each other with `--dual-strategy` (or `--ds`):

### `value` (default)

FG = average color of naturally bright pixels, BG = average color of naturally dark pixels. No recoloring — both colors come directly from the source image.

```bash
consoleimage photo.jpg -D --ds value
```

**Best for:** Photorealistic images, portraits, landscapes. Produces the most accurate representation of the source.

### `complement`

FG = source pixel average (unchanged). BG = the complementary hue (180° shift on the color wheel), darkened to 30% lightness.

```bash
consoleimage photo.jpg -D --ds complement
```

**Best for:** Colorful animations, logos, artwork. Creates a vivid glow/halo effect where the background contrasts dramatically with the foreground dots. The darkened complement ensures the background never overwhelms the dots.

### `warmcool`

Warm pixels (red/yellow-dominated) become FG; cool pixels (blue-dominated) become BG. If the collected warm group would end up as BG, the two groups are swapped so warm is always foreground.

```bash
consoleimage photo.jpg -D --ds warmcool
```

Warmth is calculated as: `(R + 0.5×G − B) × 0.5`

**Best for:** Scenes with natural depth cues — firelight, sunsets, indoor scenes. Exploits the perceptual phenomenon where warm colors appear to advance and cool colors recede, adding a sense of depth.

### `saturate`

FG saturation is boosted 30%; BG saturation is reduced to 40% (60% desaturation). Hue and lightness are preserved for both.

```bash
consoleimage photo.jpg -D --ds saturate
```

**Best for:** Bold graphics, cartoons, high-contrast images. The hyper-saturated dots pop against the muted background, creating a stylized poster effect.

---

## Strategy Comparison

All four strategies applied to the same animated source:

| Value (default) | Complement | WarmCool | Saturate |
|---|---|---|---|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_dual_value.gif" width="180" alt="Dual Value"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_dual_complement.gif" width="180" alt="Dual Complement"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_dual_warmcool.gif" width="180" alt="Dual WarmCool"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_dual_saturate.gif" width="180" alt="Dual Saturate"> |
| Photorealistic | High contrast glow | Depth perception | Stylized pop |

Compare dual-color against standard braille on the same source:

| Standard Braille | Dual-Color (value) |
|---|---|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_braille.gif" width="250" alt="Standard Braille"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_dual_value.gif" width="250" alt="Dual-Color Braille"> |
| Single FG color, terminal BG shows through | Independent FG+BG from image data |

---

## Technical Details

### ANSI Sequence

Each cell in dual-color mode emits a 24-bit FG + 24-bit BG sequence:

```
ESC[38;2;<R>;<G>;<B>;48;2;<R>;<G>;<B>m<braille char>
```

This is 6 bytes of color data per cell vs 3 bytes in standard mode — the output is roughly 2× larger.

### Color Stability (Flicker Reduction)

For video and animations, dual-color mode participates in the same temporal stability system as standard braille. When `--stabilize` is enabled (or `--colors N` is set), both FG and BG colors are snapped to a coarse palette grid after averaging:

```bash
consoleimage movie.mp4 --dual --stabilize     # Snap colors to stability grid
consoleimage movie.mp4 --dual --colors 64     # Limit to 64 colors palette
```

This prevents the small frame-to-frame drift in averaged per-cell colors that causes flicker.

### Terminal Background Detection

Dual-color mode always emits an explicit ANSI background color for every cell — it never outputs a bare space character that would expose the terminal's own background. This means dual-color output looks correct on any terminal color scheme, whether dark, light, or custom.

### Performance

Dual-color rendering adds minimal overhead vs standard braille:
- One extra pass over the 8 pixels per cell (already loaded for the FG average)
- One extra color strategy call per cell
- ~2× ANSI output size (compresses well in `.cidz` files)

---

## Tips and Tricks

**For video:** `--stabilize` is highly recommended in dual-color mode, as two independent color averages per cell are more susceptible to frame-to-frame drift than a single average.

```bash
consoleimage movie.mp4 --dual --stabilize
```

**For photos with transparency:** Dual-color mode respects the terminal background detection. If your terminal uses a light background, transparent pixels in PNG/WebP images will blend correctly against it rather than showing as black.

**For GIF output:** Dual-color GIFs are saved with a 256-color palette. The two-color-per-cell nature means more unique colors per frame; use `--gif-colors 256` (the default max) for best quality.

**Choosing a strategy:**

| Image type | Recommended strategy |
|---|---|
| Photo / portrait | `value` |
| Cartoon / animation | `complement` or `saturate` |
| Landscape / nature | `warmcool` |
| Logo / graphic | `saturate` |
| Sci-fi / neon | `complement` |

---

## Combining with Other Options

Dual-color works with all standard options:

```bash
# Dual-color with custom dimensions
consoleimage photo.jpg --dual -w 120 -h 40

# Dual-color video with temporal stability and subtitles
consoleimage movie.mp4 --dual --stabilize --subs auto

# Dual-color slideshow with 5-second hold
consoleimage ./photos --dual --ds saturate --delay 5

# Save dual-color video as compressed document
consoleimage movie.mp4 --dual -o movie_dual.cidz

# Dual-color with reduced palette for retro look
consoleimage photo.jpg --dual --colors 16

# Combine with gamma/contrast tuning
consoleimage photo.jpg --dual --gamma 1.2 --contrast 1.1
```

**See also:** [CLI Guide](CLI-GUIDE.md) | [Braille Rendering](BRAILLE-RENDERING.md) | [Library API](LIBRARY-API.md)
