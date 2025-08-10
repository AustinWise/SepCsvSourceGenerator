; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
CSVGEN010 | Usage | Error | Unexpected parameter type
CSVGEN011 | Usage | Error | Missing reader parameter
CSVGEN012 | Usage | Error | Missing header parameter
CSVGEN013 | Usage | Error | Duplicate CancellationToken Parameter
CSVGEN014 | Usage | Error | Duplicate Header Parameter
CSVGEN015 | Usage | Error | Duplicate Reader Parameter
CSVGEN016 | Usage | Error | Unexpected IEnumerable parameter
CSVGEN017 | Usage | Error | Unexpected IAsyncEnumerable parameter

### Removed Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
CSVGEN003 | Usage | Error | Invalid method parameters
