using Microsoft.CodeAnalysis;

namespace SepCsvSourceGenerator;

[Generator]
public class CsvGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
    }
}
