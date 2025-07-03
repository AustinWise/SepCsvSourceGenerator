namespace SepCsvSourceGenerator;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class CsvHeaderNameAttribute(string name) : CsvAttribute
{
    public string Name { get; } = name;
}
