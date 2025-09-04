## FEATURE:

Extract core rendering logic to separate module, introduce new projects:
- rendering - core rendering logic
- benchmark - CLI app inheriting features of RenderCli project
- render-server - ASP netcore application with basic fractal rendering API:
  - it should accept GET and POST requests with fractal type, resolution, pattern, iteration count. Upon rendering it returns bitmap image as response
  - internally it maintains rendering queue to keep GPU load under control. Basic parameters for queuing such as number of parallel jobs, maximal wait time, maximal queue length are defined via app settings.

Note:
- Create new solution file referencing new projects, remove any other existing solution files.
- update dotnet to version 9.0, use slnx solution file format
- Do not touch FractalBrowser project at all.

## EXAMPLES:

Payload for POST request
```javascript
{
    fractalType: 'lyapunov',
    width: 2000,
    height: 2000,
    startA: 1,
    endA: 4,
    startB: 1,
    endB: 4,
    initial: 0.5,
    pattern: "ab",
    warmup: 10,
    iterations: 1000,
    contrast: 2
}
```

## OTHER CONSIDERATIONS:

- Focus on clean structure and simplicity for now. More PRs will be created later in order to improve various aspects of the application.