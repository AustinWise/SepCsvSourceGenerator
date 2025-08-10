using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace AWise.SepCsvSourceGenerator;

internal record CsvMethodDefinition(
    IMethodSymbol MethodSymbol,
    INamedTypeSymbol ContainingClassSymbol,
    INamedTypeSymbol ItemTypeSymbol,
    INamedTypeSymbol ReaderParameterType,
    bool IsAsync,
    string ReaderParameterName,
    string? HeaderParameterName,
    string? CancellationTokenParameterName,
    ImmutableList<CsvPropertyDefinition> PropertiesToParse);
