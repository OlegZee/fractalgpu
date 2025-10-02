# Task Completion Checklist for FractalGpu

## Before Marking Task Complete

### Code Quality
- [ ] Code follows the established style conventions (implicit usings, nullable reference types)
- [ ] Proper error handling and logging implemented
- [ ] Resource disposal implemented where required (especially for GPU resources)
- [ ] Code is properly documented with XML comments for public APIs
- [ ] Unit tests written and passing (if applicable)
- [ ] Integration tests pass (if applicable)

### Testing
- [ ] Changes don't break existing functionality
- [ ] New functionality works as expected
- [ ] Performance hasn't significantly degraded (especially for rendering functions)
- [ ] Run the benchmark to ensure performance characteristics are maintained

### Compatibility
- [ ] Code works with both net9.0 and net8.0 targets (as defined in the project file)
- [ ] macOS-specific OpenCL pathing considerations addressed if relevant
- [ ] Cross-platform compatibility maintained where applicable

### Build Process
- [ ] Solution builds without errors: `dotnet build`
- [ ] All projects compile successfully
- [ ] No warnings introduced by new code

### Final Checks
- [ ] Run the affected application to verify functionality (e.g., benchmark, web API, or GUI)
- [ ] Commit changes with a meaningful commit message
- [ ] Ensure no secrets or temporary files are included in the commit