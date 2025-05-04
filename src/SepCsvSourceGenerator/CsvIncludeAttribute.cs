namespace SepCsvSourceGenerator;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
internal class CsvIncludeAttribute : CsvAttribute
{
}
