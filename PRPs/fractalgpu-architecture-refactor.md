name: "FractalGPU Architecture Refactor - Modular Rendering with ASP.NET Core API"
description: |

## Purpose
Refactor FractalGPU from monolithic structure to modular architecture with separate rendering library, CLI benchmark tool, and ASP.NET Core rendering API server.

## Core Principles
1. **Context is King**: Include ALL necessary documentation, examples, and caveats
2. **Validation Loops**: Provide executable tests/lints the AI can run and fix
3. **Information Dense**: Use keywords and patterns from the codebase
4. **Progressive Success**: Start simple, validate, then enhance
5. **Global rules**: Be sure to follow all rules in CLAUDE.md

---

## Goal
Extract core rendering logic into a separate module and create three new projects:
1. **rendering** - Core rendering logic library
2. **benchmark** - CLI app inheriting features of RenderCli project  
3. **render-server** - ASP.NET Core application with fractal rendering API

All projects should target .NET 9.0 and use the new SLNX solution file format.

## Why
- **Separation of Concerns**: Core rendering logic becomes reusable across applications
- **API Access**: Enable web-based fractal generation for external consumers
- **Modern Architecture**: Leverage latest .NET 9.0 features and SLNX format
- **Scalability**: Background queue processing prevents GPU overload

## What
- Extract rendering engine into shared library
- Create modern CLI benchmark tool
- Build REST API with GET/POST endpoints accepting fractal parameters
- Implement background job queue for GPU load management
- Return bitmap images as HTTP responses

### Success Criteria
- [ ] Core rendering logic extracted to separate library project
- [ ] CLI benchmark app works identically to existing RenderCli
- [ ] ASP.NET Core API accepts fractal parameters and returns bitmap images
- [ ] Background queue manages GPU load with configurable parameters
- [ ] All projects target .NET 9.0 with SLNX solution file
- [ ] FractalBrowser project remains untouched

## All Needed Context

### Documentation & References
```yaml
# MUST READ - Include these in your context window
- url: https://devblogs.microsoft.com/dotnet/introducing-slnx-support-dotnet-cli/
  why: Understanding new SLNX solution file format for .NET 9.0
  
- url: https://learn.microsoft.com/en-us/dotnet/core/extensions/queue-service
  why: Background task queue patterns for GPU load management
  
- url: https://stackoverflow.com/questions/40794275/return-jpeg-image-from-asp-net-core-webapi
  why: Returning bitmap images from ASP.NET Core controllers
  
- url: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-9.0
  why: Background services for task queue processing
  
- file: src/RenderCli/Fractal/LyapRendererBase.cs
  why: Core rendering architecture and strategy pattern implementation
  
- file: src/RenderCli/Fractal/Lyapunov.Settings.cs  
  why: Fractal parameter structure and fluent API patterns
  
- file: src/RenderCli/Program.cs
  why: Current benchmark implementation and renderer factory pattern
  
- file: src/RenderCli/RenderCli.csproj
  why: Current project structure and dependencies (Cloo.clSharp)
```

### Current Codebase tree
```bash
src/
├── FractalBrowser/           # Legacy GUI app (DO NOT TOUCH)
│   ├── Common/              # Shared utilities (duplicated)
│   ├── Fractal/             # Renderer implementations (duplicated)
│   └── View/                # Windows Forms UI
├── RenderCli/               # Modern CLI benchmark app
│   ├── Common/              # Shared utilities (Range, Sz)
│   ├── Fractal/             # Renderer implementations
│   │   ├── LyapRendererBase.cs      # Abstract base class
│   │   ├── LyapRendererCpu.cs       # Single-threaded CPU
│   │   ├── LyapRendererMulticore.cs # Multi-threaded wrapper
│   │   ├── LyapRendererOpenCl.cs    # GPU implementation
│   │   ├── FractalRenderer.cs       # Generic base
│   │   ├── RenderSettings.cs        # Base settings
│   │   └── Lyapunov.Settings.cs     # Lyapunov-specific settings
│   ├── Media/               # Bitmap handling
│   │   └── RawBitmap.cs    # Bitmap output format
│   ├── Resources/           # Embedded OpenCL kernels
│   └── Program.cs           # Current benchmark implementation
└── FractalBrowser.sln       # Legacy solution file
```

### Desired Codebase tree with files to be added and responsibility of file
```bash
src/
├── FractalGpu.Rendering/     # NEW - Core rendering library
│   ├── Common/              # Utilities (Range, Sz) 
│   ├── Fractal/             # All renderer implementations
│   ├── Media/               # Bitmap handling
│   ├── Resources/           # OpenCL kernels
│   └── FractalGpu.Rendering.csproj
├── FractalGpu.Benchmark/     # NEW - CLI benchmark app
│   ├── Program.cs           # Benchmark logic
│   └── FractalGpu.Benchmark.csproj
├── FractalGpu.RenderServer/  # NEW - ASP.NET Core API
│   ├── Controllers/         
│   │   └── FractalController.cs     # API endpoints
│   ├── Services/
│   │   ├── IRenderQueue.cs          # Queue interface
│   │   ├── RenderQueue.cs           # Queue implementation
│   │   └── RenderBackgroundService.cs # Background processor
│   ├── Models/
│   │   ├── FractalRequest.cs        # Request DTOs
│   │   └── QueueSettings.cs         # Queue configuration
│   ├── Program.cs           # API startup
│   ├── appsettings.json     # Configuration
│   └── FractalGpu.RenderServer.csproj
├── FractalBrowser/          # Existing - DO NOT TOUCH
└── FractalGpu.slnx          # NEW - .NET 9.0 solution file
```

### Known Gotchas of our codebase & Library Quirks
```csharp
// CRITICAL: OpenCL setup required on macOS
// Must set DYLD_LIBRARY_PATH=/System/Library/Frameworks/OpenCL.framework
// See Program.cs RuntimeInformation.IsOSPlatform(OSPlatform.OSX) block

// CRITICAL: Cloo.clSharp dependency for OpenCL
// Project currently uses Cloo.clSharp v1.0.1 NuGet package
// Alternative local project reference available but commented out

// GOTCHA: LyapRendererMulticore is a wrapper around any renderer
// Generic type parameter <T> where T : LyapRendererBase
// Constructor takes threadCount parameter (currently hardcoded to 256)

// PATTERN: Settings use fluent API with immutable updates
// settings.SetA(2, 4).SetB(2, 4).SetPattern("ab")
// Each Set method returns new Settings instance

// CRITICAL: RawBitmap.Save() method for file output
// Core output format - must be preserved in API responses

// PERFORMANCE: Benchmark uses progressive scaling
// Starts with 256x256, 1000 iterations, scales up until >2.5s execution time
```

## Implementation Blueprint

### Data models and structure

Create the core data models to ensure type safety and consistency between API and rendering engine.

```csharp
// API Request/Response models
public class FractalRequest
{
    public string FractalType { get; set; } = "lyapunov";
    public int Width { get; set; }
    public int Height { get; set; }
    public double StartA { get; set; }
    public double EndA { get; set; }
    public double StartB { get; set; }
    public double EndB { get; set; }
    public double Initial { get; set; }
    public string Pattern { get; set; } = "ab";
    public int Warmup { get; set; }
    public int Iterations { get; set; }
    public double Contrast { get; set; }
}

// Queue Configuration
public class QueueSettings
{
    public int MaxParallelJobs { get; set; } = 2;
    public TimeSpan MaxWaitTime { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxQueueLength { get; set; } = 10;
}
```

### List of tasks to be completed to fulfill the PRP in the order they should be completed

```yaml
Task 1: Create SLNX solution file
CREATE FractalGpu.slnx:
  - Use dotnet new sln --format slnx
  - Target .NET 9.0 projects
  - Do NOT include FractalBrowser project

Task 2: Create rendering library project
CREATE src/FractalGpu.Rendering/:
  - COPY all files from src/RenderCli/Common/, Fractal/, Media/, Resources/
  - MODIFY FractalGpu.Rendering.csproj targeting net9.0
  - PRESERVE all existing renderer implementations
  - KEEP Cloo.clSharp dependency

Task 3: Create benchmark CLI project  
CREATE src/FractalGpu.Benchmark/:
  - COPY Program.cs from src/RenderCli/
  - MODIFY to reference FractalGpu.Rendering project
  - PRESERVE existing benchmark behavior exactly
  - KEEP macOS OpenCL setup code

Task 4: Create ASP.NET Core API project
CREATE src/FractalGpu.RenderServer/:
  - Use dotnet new webapi template
  - ADD project reference to FractalGpu.Rendering
  - TARGET net9.0 framework

Task 5: Implement FractalController
CREATE src/FractalGpu.RenderServer/Controllers/FractalController.cs:
  - PATTERN: Standard ASP.NET Core controller with dependency injection
  - IMPLEMENT GET and POST endpoints accepting FractalRequest
  - RETURN File() result with bitmap and "image/bmp" MIME type
  - INTEGRATE with render queue service

Task 6: Implement render queue service
CREATE src/FractalGpu.RenderServer/Services/IRenderQueue.cs:
  - DEFINE interface for queuing render jobs
  - METHODS: QueueRenderAsync, GetQueueStatus

CREATE src/FractalGpu.RenderServer/Services/RenderQueue.cs:
  - PATTERN: Channel<T> based queue implementation  
  - IMPLEMENT thread-safe queue with configurable limits
  - HANDLE queue full scenarios gracefully

Task 7: Implement background service
CREATE src/FractalGpu.RenderServer/Services/RenderBackgroundService.cs:
  - INHERIT BackgroundService base class
  - PATTERN: Continuous dequeue and process loop
  - INSTANTIATE appropriate renderer based on configuration
  - HANDLE exceptions and logging

Task 8: Configure dependency injection and settings
MODIFY src/FractalGpu.RenderServer/Program.cs:
  - REGISTER services: IRenderQueue, RenderQueue, RenderBackgroundService
  - CONFIGURE QueueSettings from appsettings.json
  - ENABLE controllers and configure routing

CREATE src/FractalGpu.RenderServer/appsettings.json:
  - ADD QueueSettings configuration section
  - SET reasonable defaults for MaxParallelJobs, MaxWaitTime, MaxQueueLength

Task 9: Update solution file
MODIFY FractalGpu.slnx:
  - ADD all three new projects to solution
  - VERIFY build and test configuration
```

### Per task pseudocode as needed added to each task

```csharp
// Task 5: FractalController Implementation
[ApiController]
[Route("api/[controller]")]
public class FractalController : ControllerBase
{
    private readonly IRenderQueue _renderQueue;
    
    [HttpPost("render")]
    public async Task<IActionResult> RenderFractal([FromBody] FractalRequest request)
    {
        // PATTERN: Validate input parameters
        if (!ModelState.IsValid) return BadRequest(ModelState);
        
        // CRITICAL: Convert API model to Settings
        var settings = new Lyapunov.Settings()
            .SetA(request.StartA, request.EndA)
            .SetB(request.StartB, request.EndB)
            .SetPattern(request.Pattern)
            .SetInitial(request.Initial)
            .SetIterations(request.Warmup, request.Iterations)
            .SetSize(new Sz(request.Width, request.Height))
            .SetContrast(request.Contrast);
            
        // PATTERN: Queue the render job
        var result = await _renderQueue.QueueRenderAsync(settings);
        
        // CRITICAL: Return File() with proper MIME type
        return File(result.ToByteArray(), "image/bmp");
    }
    
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var status = _renderQueue.GetQueueStatus();
        return Ok(status);
    }
}

// Task 6: RenderQueue Implementation  
public class RenderQueue : IRenderQueue
{
    private readonly Channel<QueuedRenderJob> _queue;
    private readonly QueueSettings _settings;
    
    public async Task<RawBitmap> QueueRenderAsync(Lyapunov.Settings settings)
    {
        // PATTERN: Channel-based async queue
        var completionSource = new TaskCompletionSource<RawBitmap>();
        var job = new QueuedRenderJob { Settings = settings, CompletionSource = completionSource };
        
        // GOTCHA: Handle queue full scenario
        if (!_queue.Writer.TryWrite(job))
        {
            throw new InvalidOperationException("Render queue is full");
        }
        
        // CRITICAL: Implement timeout handling
        using var timeoutCts = new CancellationTokenSource(_settings.MaxWaitTime);
        return await completionSource.Task.WaitAsync(timeoutCts.Token);
    }
}

// Task 7: Background Service
public class RenderBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // PATTERN: Continuous processing loop
        await foreach (var job in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                // CRITICAL: Use appropriate renderer (GPU preferred)
                var renderer = new LyapRendererOpenCl();
                var result = renderer.Render(job.Settings);
                job.CompletionSource.SetResult(result);
            }
            catch (Exception ex)
            {
                job.CompletionSource.SetException(ex);
            }
        }
    }
}
```

### Integration Points
```yaml
DEPENDENCIES:
  - rendering: Cloo.clSharp v1.0.1 (OpenCL wrapper)
  - server: Microsoft.AspNetCore.App (ASP.NET Core 9.0)
  - benchmark: Console application targeting net9.0
  
SOLUTION:
  - format: SLNX (new .NET 9.0 XML format)
  - command: "dotnet new sln --format slnx --name FractalGpu"
  
CONFIG:
  - add to: appsettings.json
  - pattern: |
    {
      "QueueSettings": {
        "MaxParallelJobs": 2,
        "MaxWaitTime": "00:05:00",
        "MaxQueueLength": 10
      }
    }
  
ROUTES:
  - POST: /api/fractal/render (accepts FractalRequest JSON)
  - GET: /api/fractal/status (returns queue status)
```

## Validation Loop

### Level 1: Syntax & Style
```bash
# Run these FIRST - fix any errors before proceeding
dotnet build                         # Build all projects in solution
dotnet format                        # Auto-format code
dotnet restore                       # Restore NuGet packages

# macOS OpenCL setup if testing GPU renderer
export DYLD_LIBRARY_PATH=$DYLD_LIBRARY_PATH:/System/Library/Frameworks/OpenCL.framework

# Expected: No compilation errors. If errors, READ the error and fix.
```

### Level 2: Unit Tests each new feature/file/function use existing test patterns
```csharp
// CREATE FractalControllerTests.cs with these test cases:
[Test]
public async Task RenderFractal_ValidRequest_ReturnsFile()
{
    var request = new FractalRequest 
    { 
        Width = 256, Height = 256, StartA = 2, EndA = 4, 
        StartB = 2, EndB = 4, Pattern = "ab", Initial = 0.5,
        Warmup = 10, Iterations = 1000, Contrast = 1.7
    };
    
    var result = await _controller.RenderFractal(request);
    Assert.That(result, Is.InstanceOf<FileResult>());
}

[Test] 
public async Task RenderFractal_InvalidInput_ReturnsBadRequest()
{
    var request = new FractalRequest { Width = -1 };
    
    var result = await _controller.RenderFractal(request);
    Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
}

[Test]
public async Task QueueRender_QueueFull_ThrowsException()
{
    // Fill queue to capacity then test overflow behavior
    Assert.ThrowsAsync<InvalidOperationException>(
        () => _renderQueue.QueueRenderAsync(validSettings));
}
```

```bash
# Run and iterate until passing:
dotnet test --verbosity normal
# If failing: Read error, understand root cause, fix code, re-run
```

### Level 3: Integration Test
```bash
# Start the API server
cd src/FractalGpu.RenderServer
dotnet run

# Test the POST endpoint
curl -X POST http://localhost:5000/api/fractal/render \
  -H "Content-Type: application/json" \
  -d '{
    "fractalType": "lyapunov",
    "width": 256, "height": 256,
    "startA": 2, "endA": 4,
    "startB": 2, "endB": 4,
    "initial": 0.5, "pattern": "ab",
    "warmup": 10, "iterations": 1000,
    "contrast": 1.7
  }' \
  --output test_fractal.bmp

# Test queue status endpoint  
curl http://localhost:5000/api/fractal/status

# Test CLI benchmark still works
cd ../FractalGpu.Benchmark
dotnet run

# Expected: 
# - POST returns bitmap file (test_fractal.bmp created)
# - GET returns JSON with queue statistics
# - CLI produces same benchmark output as original
```

## Final validation Checklist
- [ ] All tests pass: `dotnet test --verbosity normal`
- [ ] No compilation errors: `dotnet build` 
- [ ] Code formatting applied: `dotnet format`
- [ ] API responds to POST with bitmap: `curl -X POST .../render`
- [ ] Queue status endpoint works: `curl .../status`
- [ ] CLI benchmark matches original performance
- [ ] SLNX solution file loads correctly
- [ ] All projects target .NET 9.0
- [ ] FractalBrowser project untouched

---

## Anti-Patterns to Avoid
- ❌ Don't modify FractalBrowser project or its dependencies
- ❌ Don't change core rendering algorithms or performance characteristics  
- ❌ Don't use sync methods in async API controller actions
- ❌ Don't hardcode queue settings - use appsettings.json configuration
- ❌ Don't forget macOS OpenCL path setup in documentation
- ❌ Don't skip proper exception handling in background service
- ❌ Don't create memory leaks with bitmap disposal in API responses
- ❌ Don't ignore queue capacity limits - handle graceful degradation
- ❌ Don't break existing CLI benchmark behavior or performance

---

## Confidence Score: 8/10

This PRP provides comprehensive context for one-pass implementation success:
- ✅ Complete codebase analysis with file-by-file breakdown
- ✅ Real code examples from existing implementation  
- ✅ Specific .NET 9.0 and SLNX format guidance with URLs
- ✅ Background service patterns with Channel-based queuing
- ✅ ASP.NET Core File return patterns for bitmap responses
- ✅ Detailed validation steps with executable commands
- ✅ Integration test scenarios with curl examples
- ✅ Clear task ordering to prevent dependency issues

Potential risks (reducing from 10/10):
- Complex multi-project refactoring with shared dependencies
- OpenCL/GPU renderer integration complexities across platforms