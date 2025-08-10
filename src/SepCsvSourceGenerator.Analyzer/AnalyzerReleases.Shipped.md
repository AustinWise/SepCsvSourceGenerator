; Shipped analyzer releases
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 0.3.0

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

## Release 0.2.1

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
CSVGEN009 | Usage | Error | Missing header name

## Release 0.1.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
CSVGEN001 | Usage | Error | Method must be partial and static
CSVGEN002 | Usage | Error | Invalid return type
CSVGEN003 | Usage | Error | Invalid method parameters
CSVGEN004 | Usage | Error | Missing CsvDateFormat attribute
CSVGEN005 | Usage | Error | Essential types not found
CSVGEN006 | Usage | Error | Invalid header name
CSVGEN007 | Usage | Warning | No properties to parse
CSVGEN008 | Usage | Error | Property not parsable
