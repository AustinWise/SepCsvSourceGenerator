namespace SepCsvSourceGenerator;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class CsvDateFormatAttribute(string format) : CsvAttribute
{
    public string Format { get; } = format;
}
