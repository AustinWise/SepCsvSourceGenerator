namespace SepCsvSourceGenerator;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class CsvHeaderNameAttribute : CsvAttribute
{
    public CsvHeaderNameAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }
}
