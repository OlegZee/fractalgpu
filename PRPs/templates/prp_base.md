name: "Base PRP Template v2 - Context-Rich with Validation Loops"
description: |

## Purpose
Template optimized for AI agents to implement features with sufficient context and self-validation capabilities to achieve working code through iterative refinement.

## Core Principles
1. **Context is King**: Include ALL necessary documentation, examples, and caveats
2. **Validation Loops**: Provide executable tests/lints the AI can run and fix
3. **Information Dense**: Use keywords and patterns from the codebase
4. **Progressive Success**: Start simple, validate, then enhance
5. **Global rules**: Be sure to follow all rules in CLAUDE.md

---

## Goal
[What needs to be built - be specific about the end state and desires]

## Why
- [Business value and user impact]
- [Integration with existing features]
- [Problems this solves and for whom]

## What
[User-visible behavior and technical requirements]

### Success Criteria
- [ ] [Specific measurable outcomes]

## All Needed Context

### Documentation & References (list all context needed to implement the feature)
```yaml
# MUST READ - Include these in your context window
- url: [Official API docs URL]
  why: [Specific sections/methods you'll need]
  
- file: [path/to/example.cs]
  why: [Pattern to follow, gotchas to avoid]
  
- doc: [Library documentation URL] 
  section: [Specific section about common pitfalls]
  critical: [Key insight that prevents common errors]

- docfile: [PRPs/ai_docs/file.md]
  why: [docs that the user has pasted in to the project]

```

### Current Codebase tree (run `tree` in the root of the project) to get an overview of the codebase
```bash

```

### Desired Codebase tree with files to be added and responsibility of file
```bash

```

### Known Gotchas of our codebase & Library Quirks
```csharp
// CRITICAL: [Library name] requires [specific setup]
// Example: ASP.NET Core requires async methods for endpoints
// Example: Entity Framework doesn't support batch inserts over 1000 records
// Example: We use .NET 8 and System.Text.Json for serialization
```

## Implementation Blueprint

### Data models and structure

Create the core data models, we ensure type safety and consistency.
```csharp
Examples: 
 - Entity Framework models
 - DTOs (Data Transfer Objects)
 - Domain models
 - Validation attributes

```

### list of tasks to be completed to fullfill the PRP in the order they should be completed

```yaml
Task 1:
MODIFY src/ExistingModule.cs:
  - FIND pattern: "class OldImplementation"
  - INJECT after line containing "public OldImplementation()"
  - PRESERVE existing method signatures

CREATE src/NewFeature.cs:
  - MIRROR pattern from: src/SimilarFeature.cs
  - MODIFY class name and core logic
  - KEEP error handling pattern identical

...(...)

Task N:
...

```


### Per task pseudocode as needed added to each task
```csharp

// Task 1
// Pseudocode with CRITICAL details dont write entire code
public async Task<Result> NewFeatureAsync(string param)
{
    // PATTERN: Always validate input first (see src/Validators.cs)
    var validated = ValidateInput(param);  // throws ValidationException
    
    // GOTCHA: This library requires connection pooling
    using var connection = await GetConnectionAsync();  // see src/Database/ConnectionPool.cs
    
    // PATTERN: Use existing retry policy
    var retryPolicy = Policy
        .Handle<HttpRequestException>()
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    
    var result = await retryPolicy.ExecuteAsync(async () =>
    {
        // CRITICAL: API returns 429 if >10 req/sec
        await _rateLimiter.WaitAsync();
        return await _externalApiClient.CallAsync(validated);
    });
    
    // PATTERN: Standardized response format
    return FormatResponse(result);  // see src/Utils/ResponseFormatter.cs
}
```

### Integration Points
```yaml
DATABASE:
  - migration: "Add column 'FeatureEnabled' to Users table"
  - index: "CREATE INDEX IX_Users_FeatureId ON Users(FeatureId)"
  
CONFIG:
  - add to: appsettings.json
  - pattern: "\"FeatureTimeout\": 30"
  
ROUTES:
  - add to: src/Controllers/FeatureController.cs  
  - pattern: "[Route(\"api/[controller]\")]"
```

## Validation Loop

### Level 1: Syntax & Style
```bash
# Run these FIRST - fix any errors before proceeding
dotnet build                         # Build and check for compilation errors
dotnet format                        # Auto-format code
dotnet run --project Tools/CodeAnalysis  # Custom analyzers if any

# Expected: No errors. If errors, READ the error and fix.
```

### Level 2: Unit Tests each new feature/file/function use existing test patterns
```csharp
// CREATE NewFeatureTests.cs with these test cases:
[Test]
public async Task NewFeature_HappyPath_ReturnsSuccess()
{
    // Basic functionality works
    var result = await _newFeatureService.NewFeatureAsync("valid_input");
    Assert.That(result.Status, Is.EqualTo("success"));
}

[Test]
public async Task NewFeature_InvalidInput_ThrowsValidationException()
{
    // Invalid input throws ValidationException
    Assert.ThrowsAsync<ValidationException>(
        () => _newFeatureService.NewFeatureAsync(""));
}

[Test]
public async Task NewFeature_ExternalApiTimeout_HandlesGracefully()
{
    // Handles timeouts gracefully
    _mockExternalApi.Setup(x => x.CallAsync(It.IsAny<string>()))
        .ThrowsAsync(new TimeoutException());
    
    var result = await _newFeatureService.NewFeatureAsync("valid");
    Assert.That(result.Status, Is.EqualTo("error"));
    Assert.That(result.Message, Does.Contain("timeout"));
}
```

```bash
# Run and iterate until passing:
dotnet test --filter "NewFeatureTests" --verbosity normal
# If failing: Read error, understand root cause, fix code, re-run (never mock to pass)
```

### Level 3: Integration Test
```bash
# Start the service
dotnet run --project src/YourProject

# Test the endpoint
curl -X POST http://localhost:5000/api/feature \
  -H "Content-Type: application/json" \
  -d '{"param": "test_value"}'

# Expected: {"status": "success", "data": {...}}
# If error: Check logs in console output or logs/app.log for stack trace
```

## Final validation Checklist
- [ ] All tests pass: `dotnet test --verbosity normal`
- [ ] No compilation errors: `dotnet build`
- [ ] Code formatting applied: `dotnet format`
- [ ] Manual test successful: [specific curl/command]
- [ ] Error cases handled gracefully
- [ ] Logs are informative but not verbose
- [ ] Documentation updated if needed

---

## Anti-Patterns to Avoid
- ❌ Don't create new patterns when existing ones work
- ❌ Don't skip validation because "it should work"  
- ❌ Don't ignore failing tests - fix them
- ❌ Don't use sync methods in async context without ConfigureAwait(false)
- ❌ Don't hardcode values that should be in appsettings.json
- ❌ Don't catch all exceptions - be specific with exception types
- ❌ Don't forget to dispose IDisposable resources