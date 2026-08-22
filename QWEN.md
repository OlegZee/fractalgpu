# FractalGpu Project

## Overview
FractalGpu is a .NET 10.0 project focused on high-performance rendering of Lyapunov fractals using multiple computational approaches: CPU, multi-core CPU, and GPU (OpenCL). The project consists of several interconnected components designed for different use cases, including a core rendering library, a benchmarking tool, a web API server, and a GUI application.

## Project Structure
The project is organized as a solution with multiple projects:

- `FractalGpu.Rendering`: Core rendering library with CPU, multi-core and GPU implementations
- `FractalGpu.Benchmark`: Console application for performance testing
- `FractalGpu.RenderServer`: ASP.NET Core web API for distributed rendering
- `FractalGpu.WinViewer`: .NET MAUI GUI application for interactive fractal exploration

## Core Features

### Lyapunov Fractals
- Implements Lyapunov exponent calculations for dynamical systems visualization
- Supports configurable parameters: A/B ranges, iteration pattern, initial values, warmup/iterations, contrast
- Visualizes chaos in mathematical systems with color-coded regions

### Rendering Strategies
- **CPU Renderer**: Single-threaded implementation
- **Multi-core Renderer**: Splits rendering across multiple CPU threads
- **GPU (OpenCL) Renderer**: Parallel processing on the GPU for maximum performance

### Web API Server
- REST API endpoints for rendering fractals via GET/POST requests
- Queue system to manage rendering jobs and control GPU load
- Request validation and status reporting
- Configurable queue parameters (parallel jobs, wait time, queue length)

## Building and Running

### Prerequisites
- .NET SDK 10.0 or higher
- OpenCL drivers for GPU acceleration (if using OpenCL)

### Running Components

#### Benchmark
```bash
dotnet run -c Release -p src/FractalGpu.Benchmark
```

#### Web API Server
```bash
cd src/FractalGpu.RenderServer
dotnet run
```

#### GUI Application (macOS)
```bash
cd src/FractalGpu.WinViewer
dotnet run --framework net10.0-maccatalyst --configuration Debug
```

Once built, you can double-click the .app bundle in Finder:
- Navigate to: `bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/`
- Double-click `FractalGpu.WinViewer.app`

## API Endpoints (RenderServer)
- `POST /api/fractal/render`: Accepts JSON payload with fractal parameters
- `GET /api/fractal/render`: Accepts query parameters for fractal rendering
- `GET /api/fractal/status`: Returns queue status information

Example POST payload:
```json
{
    "fractalType": "lyapunov",
    "width": 2000,
    "height": 2000,
    "startA": 1,
    "endA": 4,
    "startB": 1,
    "endB": 4,
    "initial": 0.5,
    "pattern": "ab",
    "warmup": 10,
    "iterations": 1000,
    "contrast": 2
}
```

## Development Conventions
- Uses .NET 10.0 with implicit usings and nullable reference types enabled
- Implements object-oriented patterns with base classes and inheritance
- Employs multi-threading techniques for performance optimization
- Follows MVVM pattern in the GUI application
- Includes proper resource disposal for GPU memory management
- Uses channel-based queuing for web API job management

## Key Technologies
- .NET 10.0
- OpenCL via Cloo library for GPU acceleration
- ASP.NET Core for web API
- .NET MAUI for cross-platform GUI
- System.Threading.Channels for job queuing
- LINQ and async/await patterns
