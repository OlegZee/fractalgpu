# FractalGPU

Copyright (c) 2025 FractalGPU Project. All rights reserved.

## Fractal Browser (interactive GUI)

`FractalGpu.Browser` is a cross-platform Avalonia desktop app for exploring the Lyapunov fractal interactively. It replaces the frozen WinForms `src/FractalBrowser`, which had no navigation at all — one Start button, six hard-coded regions, and a synchronous render that froze the window.

```bash
dotnet run -c Release --project src/FractalGpu.Browser
```

### Navigating

| Input | Action |
| --- | --- |
| Wheel | Zoom at the cursor |
| Drag | Pan |
| Right-drag, or Shift+drag | Zoom to a selected box |
| Double-click | Zoom in |
| Arrows, `+` / `-` | Pan and zoom from the keyboard |
| `Alt`+`←` / `Alt`+`→` | Back / forward through the browsing history |
| `Ctrl`+`0` | Back to the current preset's region |
| `Ctrl`+`S` | Render and save a PNG |

Panning and zooming resample the bitmap already on screen, so the view answers the mouse immediately while a fresh render runs in the background. Requests coalesce — dragging never queues a backlog of dead frames — and each render arrives progressively: a coarse pass first, then full resolution. On a device fast enough that the coarse pass would only add latency (a GPU renders a full 1000x750 view in ~15 ms) it is skipped.

### Panel

- **Sequence** — the `a`/`b` pattern, validated on the spot. The renderers treat every character that is not `a` as `b`, so `abx` silently renders a different fractal; the browser rejects it instead.
- **Accuracy** — iterations (log slider plus an exact field) and warm-up, which the legacy app hard-wired to `iterations/10`, plus the initial x₀ it never exposed at all.
- **Colour** — six palettes and a contrast curve. Both **recolour the cached exponent map instead of recomputing the fractal**, so they are instant even where a render takes seconds. `Classic (amber/blue)` reproduces `LyapRendererBase`'s colouring byte for byte, so the browser's default look matches what `RenderCli render` writes to a BMP.
- **Export** — a resolution multiplier; saving renders at that multiple of the viewport rather than screenshotting it.
- **Coordinates** — the current region as `minA;maxA;minB;maxB`, editable. Copy it to record or share a spot.

The status bar reports the device, render size, time and throughput; the a/b coordinates and Lyapunov exponent under the cursor; and the zoom factor. Window size, device, palette, sequence and last region are remembered between launches.

### Choosing a device

The device list is the same one `RenderCli list-devices` prints. Two things are worth knowing while zooming:

- The OpenCL kernels sample the axes through `float` tables, so below roughly `2.4e-7` per pixel the GPU renders visible stair-steps while the double-precision CPU paths keep resolving detail. The status bar warns when the view crosses that threshold — switch to a CPU device to zoom deeper.
- Render sizes are rounded up to a multiple of 16 because the OpenCL kernel consumes four B-values per work item and splits rows into power-of-two chunks; ragged sizes silently drop trailing columns or rows.

## Running Benchmark

Prerequisites:
- Dotnet SDK 10.0+

```bash
dotnet run -c Release --project src/RenderCli -- benchmark
```

## RenderCli (multi-mode CLI)

`RenderCli` is a multi-mode command-line tool. It lets you list available render devices (CPU modes and OpenCL GPU devices), run the escalating render benchmark against a specific one, and render a fractal image to a BMP file.

List available devices:

```bash
dotnet run -c Release --project src/RenderCli -- list-devices
```

Run the benchmark on a specific device (single-core CPU):

```bash
dotnet run -c Release --project src/RenderCli -- benchmark --device 0
```

Run on all CPU cores:

```bash
dotnet run -c Release --project src/RenderCli -- benchmark --device 1
```

Run the optimized CPU variants (deferred log + SIMD; single-core and multi-core):

```bash
dotnet run -c Release --project src/RenderCli -- benchmark --device 2
dotnet run -c Release --project src/RenderCli -- benchmark --device 3
```

Run on the first GPU:

```bash
dotnet run -c Release --project src/RenderCli -- benchmark --device 4
```

Device indices: `0` single-core CPU, `1` multi-core CPU, `2` single-core perf, `3` multi-core perf, `4+` OpenCL devices, followed by their `(perf)` variants (one per OpenCL device, e.g. `5` on a single-GPU machine). Use `list-devices` for the authoritative list on your machine.

### Optimized GPU rendering

Each OpenCL device also appears as a `(perf)` entry (`LyapRendererOpenClPerf`), a performance-tuned GPU path that is ~2.3x faster than the regular GPU device (~750k vs ~325k mis on an Apple M5 Pro). It keeps the pattern as a bitmask in registers instead of a per-iteration global-memory load + modulo, takes one `native_log2` per 4 iterations (logging the product of |dF|) with ln2 folded into the output scale, specializes the pattern at kernel-compile time (`-D PAT_BITS/PAT_LEN/PHASE0`, falling back to a runtime bitmask for patterns longer than 32), caches the compiled program/context across renders, and builds with `-cl-fast-relaxed-math -cl-mad-enable`. The fast-math flag is the caveat: FMA contraction and reassociation perturb low bits of the chaotic recurrence, so ~6% of output bytes differ from the regular GPU device (statistically equivalent image, deterministic run-to-run on the same device, but not pixel-reproducible against the reference). Use the regular GPU device when pixel-exact output matters.

### Rendering to a file

Render a Lyapunov fractal to a BMP file (defaults: preferred device, `fractal.bmp`, 512x512, 10000 iterations, pattern `ab`):

```bash
dotnet run -c Release --project src/RenderCli -- render
dotnet run -c Release --project src/RenderCli -- render -d 0 -o scalar.bmp --size 256 --iterations 2000
```

Options: `--device`/`-d` device index, `--output`/`-o` output path, `--size` square image size, `--iterations` per-pixel iterations (warmup is iterations/10), `--pattern` Lyapunov sequence. Rendering the same image on device `0` (scalar CPU) and device `2` (perf) and comparing the files (`cmp a.bmp b.bmp`) is the quick correctness check for the optimized renderer — the outputs are byte-identical.

### Optimized CPU rendering

`LyapRendererCpuPerf` combines two independent optimizations, and the deferred log — not SIMD — is where most of the speedup comes from. It vectorizes the Lyapunov inner loop with variable-width `System.Numerics.Vector<double>`: the same code path runs 128-bit NEON on ARM64 (Apple Silicon), 256-bit AVX2 on x64, and falls back to the scalar renderer where hardware acceleration is unavailable. On AVX-512 machines set `DOTNET_MaxVectorTBitWidth=512` to unlock 512-bit vectors.

Instead of a transcendental log per iteration it uses a deferred log (Benettin-style renormalization): the `|r·(1−2x)|` derivatives are accumulated into a running product whose IEEE-754 exponent is periodically moved into an integer accumulator with exact bit operations, leaving a single `Math.Log2` per pixel. Lanes that hit special values (zero derivative, Inf/NaN) are recomputed with the scalar code, so output stays byte-identical to the scalar renderer (~9× faster single-core on Apple M-series, more on wider vectors).

Run with no `--device` (benchmarks ALL available devices sequentially and prints a comparison summary):

```bash
dotnet run -c Release --project src/RenderCli -- benchmark
```

Compare several devices in one run (both forms are equivalent):

```bash
dotnet run -c Release --project src/RenderCli -- benchmark -d 0 -d 2
dotnet run -c Release --project src/RenderCli -- benchmark -d 0 2
```

### Multi-core tiling and non-square sizes

`LyapRendererMulticore` splits the A range across 256 tiles. It used to hand every tile exactly `1/256` of the range regardless of how many rows the tile actually covered, so any height that is not a multiple of 256 rendered at the wrong scale — `--size 400` produced a wrong image, not merely a seam at the bottom. Tile ranges are now derived from each tile's real row bounds, and the tile count is clamped to the image height.

Tiled rendering still cannot be bit-exact against the single-core path: a tile re-derives its per-row step from its own sub-range, so rows inside a tile land one ulp away from the single-core coordinate, and pixels sitting exactly on a singularity can flip. That was true before this change too, for every height divisible by the tile count. What is guaranteed now is that no strip renders at a visibly different scale.

## Running under macOS

OpenCL library loading is handled automatically via `NativeLibrary.SetDllImportResolver` in the rendering code—no extra environment configuration is required.
