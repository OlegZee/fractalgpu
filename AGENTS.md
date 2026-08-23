# Repository Guidelines

## Project Structure & Module Organization
- `src/FractalGpu.Rendering/`: Source of truth for fractal logic (strategy pattern via `LyapRendererBase` with CPU, multicore, and OpenCL subclasses), plus common math helpers and embedded kernels.
- `src/FractalGpu.RenderServer/`: ASP.NET Core queue-driven API; tune behavior through `appsettings*.json`.
- `src/RenderCli/`: Multi-mode CLI wrapper around the rendering library (`benchmark`/`list-devices` subcommands)—keep edits minimal and reuse shared types.
- `src/FractalBrowser/`: Legacy WinForms browser targeting .NET Framework 3.5; **do not modify** unless the scope explicitly requires it.
- `libs/` & `packages/`: Checked-in dependencies for historical builds; coordinate before touching them.

## Build, Test, and Development Commands
- `dotnet restore`: Restore NuGet dependencies for every project.
- `dotnet build FractalGpu.slnx`: Compile Rendering, RenderServer, and CLI projects together.
- `dotnet run -c Release --project src/RenderCli -- benchmark`: Execute the escalating render benchmark and log perf metrics per renderer.
- `dotnet run --project src/FractalGpu.RenderServer/FractalGpu.RenderServer.csproj`: Launch the web API locally (`https://localhost:7043`); the `.http` file offers ready-made smoke calls.
- macOS GPU runs are supported out of the box; the rendering library resolves the OpenCL framework automatically.

## Coding Style & Naming Conventions
- Target C# 12 with file-scoped namespaces, `implicit usings`, and nullable reference types enabled in new code.
- Use four-space indentation, PascalCase for types/methods, camelCase for locals, and `_camelCase` for private fields.
- Use expression-bodied members only when they improve clarity.
- Keep comments purposeful—document non-obvious concurrency, OpenCL interop, or queue behavior rather than restating code.
- Avoid duplicating rendering logic across projects; reference `FractalGpu.Rendering` instead.

## Testing Guidelines
- No automated test suite is present yet; when adding one, base it on `dotnet test` and locate projects under `tests/` (create if absent).
- For now, validate changes by running the benchmark and issuing sample render requests (see `FractalGpu.RenderServer/FractalGpu.RenderServer.http`).
- Document manual test steps in PR descriptions until automated coverage exists.

## Commit & Pull Request Guidelines
- Follow conventional, action-oriented commit subjects (e.g., `refactor: share rendering core with CLI`).
- Commits should stay focused; avoid mixing refactors with feature work unless necessary.
- Pull requests must include: summary of changes, testing evidence (commands run/output), and any config updates required for operators.
- Link relevant issues or tasks when available, and attach screenshots for UI-affecting changes.

## Security & Configuration Tips
- Keep OpenCL paths and queue limits configurable via `appsettings.json`; avoid hard-coded environment tweaks.
- Never commit secrets or machine-specific certs—use user secrets or environment variables instead.
