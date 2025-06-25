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
        [GenerateCsvParser]
        public static partial IAsyncEnumerable<MyClass> ParseAsync(SepReader reader, CancellationToken ct = default);

        [CsvHeaderName("Transaction Date")]
        [CsvDateFormat("MM/dd/yyyy")]
        public required DateTime TransactionDate { get; init; }

        [CsvHeaderName("Amount")]
        public required int Amount { get; init; }

        [CsvHeaderName("Name")]
        public required string Name { get; init; }

        // If the column is missing, this property will not be set.
        [CsvHeaderName("Duration")]
        public float? Duration { get; set; }

        // Something that is not pared because it does not have a CsvHeaderName attribute.
        public string? SomethingElse { get; set; }
    }
}
