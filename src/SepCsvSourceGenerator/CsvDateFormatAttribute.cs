namespace SepCsvSourceGenerator;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class CsvDateFormatAttribute : CsvAttribute
{
    public CsvDateFormatAttribute(string format)
    {
        Format = format;
    }

    public string Format { get; }
}
