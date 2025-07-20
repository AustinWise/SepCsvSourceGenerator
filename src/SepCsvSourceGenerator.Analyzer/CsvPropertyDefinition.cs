namespace AWise.SepCsvSourceGenerator;

internal record CsvPropertyDefinition(
    string Name,
    string FullTypeName, // e.g., "System.Nullable<System.Int32>"
    string UnderlyingTypeName, // e.g., "System.Int32"
    string[] HeaderNames,
    string? DateFormat,
    bool IsRequiredMember,
    bool IsInitOnly,
    CsvPropertyKind Kind);