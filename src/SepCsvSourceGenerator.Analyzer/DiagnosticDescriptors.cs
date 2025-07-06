using Microsoft.CodeAnalysis;

namespace US.AWise.SepCsvSourceGenerator.Analyzer;

// TODO: consider adding support for localization.
internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor MethodNotPartial =
        new("CSVGEN001", "Method must be partial and static", "Method '{0}' must be declared as 'partial'", "Usage", DiagnosticSeverity.Error, true);
    public static readonly DiagnosticDescriptor InvalidReturnType =
        new("CSVGEN002", "Invalid return type", "Method must return 'IAsyncEnumerable<{0}>' or 'IEnumerable<{0}>'", "Usage", DiagnosticSeverity.Error, true);
    public static readonly DiagnosticDescriptor InvalidMethodParameters =
        new("CSVGEN003", "Invalid method parameters", "Method must have parameters '(SepReader reader, CancellationToken ct)'", "Usage", DiagnosticSeverity.Error, true);
    public static readonly DiagnosticDescriptor MissingDateFormatAttribute =
        new("CSVGEN004", "Missing CsvDateFormat attribute", "Property '{0}' of type DateTime or DateTime? must have a [CsvDateFormat] attribute", "Usage", DiagnosticSeverity.Error, true);
    public static readonly DiagnosticDescriptor EssentialTypesNotFound =
        new("CSVGEN005", "Essential types not found", "Essential types for source generation were not found: {0}", "Usage", DiagnosticSeverity.Error, true);
    public static readonly DiagnosticDescriptor InvalidHeaderName =
        new("CSVGEN006", "Invalid header name", "Property '{0}' has an invalid [CsvHeaderName] attribute. The header name cannot be null or whitespace.", "Usage", DiagnosticSeverity.Error, true);
    public static readonly DiagnosticDescriptor NoPropertiesFound =
        new("CSVGEN007", "No properties to parse", "The type '{0}' does not have any properties with the [CsvHeaderName] attribute", "Usage", DiagnosticSeverity.Warning, true);
}
