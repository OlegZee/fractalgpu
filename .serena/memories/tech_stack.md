# FractalGpu Tech Stack

## Core Technologies
- .NET 9.0 (with some components targeting net8.0 as well)
- C# language with modern features
- OpenCL via Cloo.clSharp library for GPU acceleration
- ASP.NET Core for web API
- .NET MAUI for cross-platform GUI
- System.Threading.Channels for job queuing

## Project Structure
- FractalGpu.Rendering: Core rendering library
- FractalGpu.Benchmark: Performance testing application
- FractalGpu.RenderServer: Web API server for distributed rendering
- FractalGpu.WinViewer: Cross-platform GUI application

## Code Conventions
- Implicit usings enabled
- Nullable reference types enabled
- Object-oriented patterns with inheritance and polymorphism
- Base class approach for renderer implementations
- LINQ and async/await patterns
- Resource disposal for GPU memory management
- Channel-based queuing for job management