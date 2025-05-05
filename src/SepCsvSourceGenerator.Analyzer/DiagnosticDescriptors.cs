using Microsoft.CodeAnalysis;

namespace SepCsvSourceGenerator.Analyzer;

internal class DiagnosticDescriptors
{
    const string CATEGORY = "SepCsvSourceGenerator";

    const string CsvParsingMethodMustBePartialMessage = "CSV parsing methods must be partial";
    public static DiagnosticDescriptor CsvParsingMethodMustBePartial { get; } = new DiagnosticDescriptor(
        id: "SEPCSV1001",
        title: CsvParsingMethodMustBePartialMessage,
        messageFormat: CsvParsingMethodMustBePartialMessage,
        category: CATEGORY,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    const string CsvParsingMethodMustBeStaticMessage = "CSV parsing methods must be static";
    public static DiagnosticDescriptor CsvParsingMethodMustBeStatic { get; } = new DiagnosticDescriptor(
        id: "SEPCSV1002",
        title: CsvParsingMethodMustBeStaticMessage,
        messageFormat: CsvParsingMethodMustBeStaticMessage,
        category: CATEGORY,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
