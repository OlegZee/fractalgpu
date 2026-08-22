# Project Summary

## Overall Goal
The user has a .NET 10.0 project focused on high-performance rendering of Lyapunov fractals using multiple computational approaches: CPU, multi-core CPU, and GPU (OpenCL).

## Key Knowledge
- **Project Structure**: The FractalGpu project consists of multiple interconnected components: FractalGpu.Rendering (core library), FractalGpu.Benchmark (console app), FractalGpu.RenderServer (ASP.NET Core web API), and FractalGpu.WinViewer (.NET MAUI GUI).
- **Rendering Strategies**: The project implements three main rendering strategies: single-threaded CPU (`LyapRendererCpu`), multi-core CPU (`LyapRendererMulticore`), and GPU (OpenCL) (`LyapRendererOpenCl`).
- **Class Hierarchy**: The renderer follows an inheritance pattern: `FractalRenderer<TSettings>` → `LyapRendererBase` → concrete implementations like `LyapRendererCpu`, `LyapRendererMulticore`, and `LyapRendererOpenCl`.
- **Build Prerequisites**: Requires .NET SDK 10.0 or higher and OpenCL drivers for GPU acceleration. On macOS, requires setting `DYLD_LIBRARY_PATH` for OpenCL to work properly.
- **Usage Context**: The `LyapRendererCpu` class is instantiated in multiple components across the project: benchmarking tool, web server, GUI application, and CLI tool.
- **Technology Stack**: .NET 10.0, OpenCL (via Cloo library), ASP.NET Core for web API, .NET MAUI for cross-platform GUI, System.Threading.Channels for job queuing.

## Recent Actions
- Analyzed the class hierarchy for the fractal renderer, identifying the abstract base classes and their concrete implementations
- Located all usages of the `LyapRendererCpu` constructor across the codebase in 5 different files across multiple projects
- Found that the `LyapRendererCpu` is used as the single-core CPU rendering option and is commonly paired with other rendering strategies for comparison purposes

## Current Plan
1. [DONE] Identify the fractal renderer class hierarchy
2. [DONE] Find all usages of LyapRendererCpu constructor across the codebase
3. [TODO] Further exploration of the rendering implementations and their performance characteristics may be needed for optimization purposes
4. [TODO] Investigate potential areas for improvement or refactoring in the rendering system

---

## Summary Metadata
**Update time**: 2025-10-02T14:06:48.497Z 
