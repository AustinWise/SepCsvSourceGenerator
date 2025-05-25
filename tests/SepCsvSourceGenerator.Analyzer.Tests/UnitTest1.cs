using nietras.SeparatedValues;

namespace SepCsvSourceGenerator.Analyzer.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {

        }
    }
    
    partial class MyClass
    {
        // [GenerateCsvParser]
        // public static partial IAsyncEnumerable<MyClass> ParseAsync(SepReader reader, CancellationToken ct = default);

        [CsvHeaderName("Transaction Date")]
        [CsvDateFormat("MM/dd/yyyy")]
        public required DateTime TransactionDate { get; init; }

        public string? SomethingElse { get; set; }
    }
}
