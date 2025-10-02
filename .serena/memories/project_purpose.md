# FractalGpu Project Purpose

FractalGpu is a .NET 9.0 project focused on high-performance rendering of Lyapunov fractals using multiple computational approaches: CPU, multi-core CPU, and GPU (OpenCL). The project allows users to visualize mathematical chaos systems by calculating and rendering Lyapunov exponents.

## Main Components

1. **FractalGpu.Rendering**: Core rendering library with CPU, multi-core and GPU implementations
2. **FractalGpu.Benchmark**: Console application for performance testing
3. **FractalGpu.RenderServer**: ASP.NET Core web API for distributed rendering
4. **FractalGpu.WinViewer**: .NET MAUI GUI application for interactive fractal exploration

## Core Features
- Lyapunov fractal visualization with configurable parameters
- Multiple rendering strategies: single-core CPU, multi-core CPU, and GPU (OpenCL)
- Web API for distributed fractal rendering
- Interactive GUI for fractal exploration
- Performance benchmarking tools