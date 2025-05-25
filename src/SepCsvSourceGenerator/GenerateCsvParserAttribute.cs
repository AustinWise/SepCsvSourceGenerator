namespace SepCsvSourceGenerator;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class GenerateCsvParserAttribute : CsvAttribute
{
    public GenerateCsvParserAttribute()
    {
    }
}
