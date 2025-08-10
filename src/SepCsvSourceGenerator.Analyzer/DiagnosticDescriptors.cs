using Microsoft.CodeAnalysis;

namespace AWise.SepCsvSourceGenerator.Analyzer;

// TODO: consider adding support for localization.
internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor MethodNotPartial =
        new("CSVGEN001", "Method must be partial and static", "Method '{0}' must be declared as 'partial'", "Usage", DiagnosticSeverity.Error, true);
    public static readonly DiagnosticDescriptor InvalidReturnType =
        new("CSVGEN002", "Invalid return type", "Method must return 'IAsyncEnumerable<{0}>' or 'IEnumerable<{0}>'", "Usage", DiagnosticSeverity.Error, true);
    // CSVGEN003: previously "Invalid method parameters"
    public static readonly DiagnosticDescriptor MissingDateFormatAttribute =
        new("CSVGEN004", "Missing CsvDateFormat attribute", "Property '{0}' of type DateTime or DateTime? must have a [CsvDateFormat] attribute", "Usage", DiagnosticSeverity.Error, true);
    public static readonly DiagnosticDescriptor EssentialTypesNotFound =
        new("CSVGEN005", "Essential types not found", "Essential types for source generation were not found: {0}", "Usage", DiagnosticSeverity.Error, true);
    public static readonly DiagnosticDescriptor InvalidHeaderName =
        new("CSVGEN006", "Invalid header name", "Property '{0}' has an invalid [CsvHeaderName] attribute. The header name cannot be null or whitespace.", "Usage", DiagnosticSeverity.Error, true);
    public static readonly DiagnosticDescriptor NoPropertiesFound =
        new("CSVGEN007", "No properties to parse", "The type '{0}' does not have any properties with the [CsvHeaderName] attribute", "Usage", DiagnosticSeverity.Warning, true);
    public static readonly DiagnosticDescriptor PropertyNotParsable =
        new("CSVGEN008", "Property not parsable", "Property '{0}' of type '{1}' is not parsable. It must be an enum or implement ISpanParsable<T>.", "Usage", DiagnosticSeverity.Error, true);
    public static readonly DiagnosticDescriptor HeaderNamesEmpty =
        new("CSVGEN009", "Missing header name", "Must specify one or more header names", "Usage", DiagnosticSeverity.Error, true);
    public static readonly DiagnosticDescriptor UnexpectedParameterType =
        new("CSVGEN010", "Unexpected parameter type", "Parameter is not of any expected type. Should be one of SepReader, SepReaderHeader, CancellationToken, IEnumerable<SepReader.Row>, or IAsyncEnumerable<SepReader.Row>.", "Usage", DiagnosticSeverity.Error, true);
    public static readonly DiagnosticDescriptor MissingReaderParameter =
        new("CSVGEN011", "Missing reader parameter", "No reader parameter specified, must specify one parameter of type SepReader, IEnumerable<SepReader.Row>, or IAsyncEnumerable<SepReader.Row>", "Usage", DiagnosticSeverity.Error, true);
    public static readonly DiagnosticDescriptor MissingHeaderParameter =
        new("CSVGEN012", "Missing header parameter", "", "Usage", DiagnosticSeverity.Error, true);
    public static readonly DiagnosticDescriptor DuplicateCancellationTokenParameter =
        new("CSVGEN013", "Duplicate CancellationToken Parameter", "Only one parameter of type CancellationToken can be specified in the parameter list", "Usage", DiagnosticSeverity.Error, true);
    public static readonly DiagnosticDescriptor DuplicateHeaderParameter =
        new("CSVGEN014", "Duplicate Header Parameter", "Only one parameter of type SepReaderHeader can be specified in the parameter list", "Usage", DiagnosticSeverity.Error, true);
    public static readonly DiagnosticDescriptor DuplicateReaderParameter =
        new("CSVGEN015", "Duplicate Reader Parameter", "Only one parameter of type SepReader, IEnumerable<SepReader.Row>, or IAsyncEnumerable<SepReader.Row> can be specified in the parameter list", "Usage", DiagnosticSeverity.Error, true);
    public static readonly DiagnosticDescriptor UnexpectedIEnumerableParameter =
        new("CSVGEN016", "Unexpected IEnumerable parameter", "If a parameter is IEnumerable, the return type must also be IEnumerable (not IAsyncEnumerable)", "Usage", DiagnosticSeverity.Error, true);
    public static readonly DiagnosticDescriptor UnexpectedIAsyncEnumerableParameter =
        new("CSVGEN017", "Unexpected IAsyncEnumerable parameter", "If a parameter is IEnumerable, the return type must also be IAsyncEnumerable (not IEnumerable)", "Usage", DiagnosticSeverity.Error, true);
}
