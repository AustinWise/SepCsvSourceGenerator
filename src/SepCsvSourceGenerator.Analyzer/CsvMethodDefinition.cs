using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace AWise.SepCsvSourceGenerator;

internal record CsvMethodDefinition(
    IMethodSymbol MethodSymbol,
    INamedTypeSymbol ContainingClassSymbol,
    INamedTypeSymbol ItemTypeSymbol,
    bool IsAsync,
    ImmutableList<CsvPropertyDefinition> PropertiesToParse);
