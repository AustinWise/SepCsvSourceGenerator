using nietras.SeparatedValues;

namespace US.AWise.SepCsvSourceGenerator.Analyzer.Tests;

public partial class CsvGeneratorParsingTests
{
    public enum MyEnum { A, B, C }
    public class BaseRecord
    {
        [CsvHeaderName("ID")]
        public int Id { get; set; }
    }
    public partial class MyRecord : BaseRecord
    {
        [CsvHeaderName("Name")]
        public required string Name { get; set; }

        [CsvHeaderName("Date")]
        [CsvDateFormat("yyyy-MM-dd")]
        public DateTime Date { get; set; }

        [CsvHeaderName("Enum")]
        public MyEnum EnumValue { get; set; }

        [CsvHeaderName("NullableInt")]
        public int? NullableInt { get; set; }

        [CsvHeaderName("MissingField")]
        public string? MissingField { get; set; }

        [GenerateCsvParser]
        public static partial IEnumerable<MyRecord> Parse(SepReader reader, CancellationToken ct);

        [GenerateCsvParser]
        public static partial IAsyncEnumerable<MyRecord> ParseAsync(SepReader reader, CancellationToken ct);
    }

    const string CSV_CONTENT = "ID,Name,Date,Enum,NullableInt\n1,John Doe,2023-01-15,A,42\n2,Jane,2023-02-20,B,123";

    [Fact]
    public void Parse()
    {
        using var reader = Sep.Reader().FromText(CSV_CONTENT);
        Verify(MyRecord.Parse(reader, CancellationToken.None).ToList());
    }

    [Fact]
    public void ParseAsync()
    {
        using var reader = Sep.Reader().FromText(CSV_CONTENT);
        Verify(MyRecord.ParseAsync(reader, CancellationToken.None).ToBlockingEnumerable().ToList());
    }

    private static void Verify(List<MyRecord> list)
    {
        Assert.Equal(2, list.Count);

        MyRecord item1 = list[0];
        Assert.Equal(1, item1.Id);
        Assert.Equal("John Doe", item1.Name);
        Assert.Equal(new DateTime(2023, 1, 15), item1.Date);
        Assert.Equal(MyEnum.A, item1.EnumValue);
        Assert.Equal(42, item1.NullableInt);
        Assert.Null(item1.MissingField);

        MyRecord item2 = list[1];
        Assert.Equal(2, item2.Id);
        Assert.Equal("Jane", item2.Name);
        Assert.Equal(new DateTime(2023, 2, 20), item2.Date);
        Assert.Equal(MyEnum.B, item2.EnumValue);
        Assert.Equal(123, item2.NullableInt);
        Assert.Null(item2.MissingField);
    }
}
