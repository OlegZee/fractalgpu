# FractalGpu Code Style and Conventions

## Language Features
- C# with .NET 9.0 features
- Implicit usings enabled (no need to explicitly include common namespaces)
- Nullable reference types enabled (use nullable annotations and checks)
- Modern C# syntax and patterns (switch expressions, pattern matching, etc.)

## Naming Conventions
- PascalCase for public/protected members, classes, and namespaces
- camelCase for private members and local variables
- Meaningful, descriptive names for variables, methods, and classes

## Architecture Patterns
- Object-oriented design with inheritance and polymorphism
- Abstract base classes (e.g., FractalRenderer<TSettings>, LyapRendererBase) for common functionality
- Generic classes where appropriate (e.g., LyapRendererMulticore<TBaseRenderer>)
- Interface-based design for services (e.g., IRenderQueue)
- MVVM pattern in the GUI application

## Coding Practices
- Proper resource disposal, especially for GPU memory management
- Async/await patterns for asynchronous operations
- Exception handling with appropriate logging
- Channel-based queuing for concurrent job processing
- LINQ for data processing when appropriate
- Dependency injection in ASP.NET Core components

## Documentation Standards
- XML documentation comments for public APIs
- Method documentation with <summary> and parameter descriptions
- Comments explaining complex algorithms or business logic
- Inline comments for non-obvious implementation details

## File Structure
- Organized by feature in subdirectories
- Clear separation between core rendering logic, UI, benchmarking, and web API components
- Common utilities and abstractions in shared locations