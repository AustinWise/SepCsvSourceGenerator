namespace AWise.SepCsvSourceGenerator;

internal record CsvPropertyDefinition(
    string Name,
    string FullTypeName, // e.g., "System.Nullable<System.Int32>"
    string UnderlyingTypeName, // e.g., "System.Int32"
    string[] HeaderNames,
    string? DateFormat,
    bool IsRequiredMember,
    bool IsInitOnly,
    CsvPropertyKind Kind,
    string? ElementTypeName = null, // For List types: the type of the elements
    CsvPropertyKind? ElementKind = null, // For List types: the kind of the elements
    string? ElementDateFormat = null, // For List types with date elements
    char ListDelimiter = ','); // For List types: the delimiter to split elements