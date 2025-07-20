using nietras.SeparatedValues;

namespace AWise.SepCsvSourceGenerator.Analyzer.Tests;

public partial class RunGeneratedParserTests
{
    public enum MyEnum { A, B, C }
    public class BaseRecord
    {
        [CsvHeaderName("ID")]
        public required int Id { get; init; }
    }
    public partial class MyRecord : BaseRecord
    {
        [CsvHeaderName("Name")]
        public required string PersonName { get; set; }

        [CsvDateFormat("yyyy-MM-dd")]
        public DateTime Date { get; set; }

        [CsvHeaderName("Enum")]
        public MyEnum EnumValue { get; set; }

        public int? NullableInt { get; set; }

        public string? MissingField { get; set; }

        [GenerateCsvParser(IncludeProperties = true)]
        public static partial IEnumerable<MyRecord> Parse(SepReader reader, CancellationToken ct);

        [GenerateCsvParser(IncludeProperties = true)]
        public static partial IAsyncEnumerable<MyRecord> ParseAsync(SepReader reader, CancellationToken ct);
    }

    const string CSV_CONTENT = "ID,Name,Date,Enum,NullableInt\n1,John Doe,2023-01-15,A,42\n2,Jane,2023-02-20,B,123";

    [Fact]
    public void Parse()
    {
        using var reader = Sep.Reader().FromText(CSV_CONTENT);
        Verify(MyRecord.Parse(reader, CancellationToken.None));
    }

    [Fact]
    public void ParseAsync()
    {
        using var reader = Sep.Reader().FromText(CSV_CONTENT);
        Verify(MyRecord.ParseAsync(reader, CancellationToken.None).ToBlockingEnumerable());
    }

    private static void Verify(IEnumerable<MyRecord> enumerable)
    {
        var list = enumerable.ToList();
        Assert.Equal(2, list.Count);

        MyRecord item1 = list[0];
        Assert.Equal(1, item1.Id);
        Assert.Equal("John Doe", item1.PersonName);
        Assert.Equal(new DateTime(2023, 1, 15), item1.Date);
        Assert.Equal(MyEnum.A, item1.EnumValue);
        Assert.Equal(42, item1.NullableInt);
        Assert.Null(item1.MissingField);

        MyRecord item2 = list[1];
        Assert.Equal(2, item2.Id);
        Assert.Equal("Jane", item2.PersonName);
        Assert.Equal(new DateTime(2023, 2, 20), item2.Date);
        Assert.Equal(MyEnum.B, item2.EnumValue);
        Assert.Equal(123, item2.NullableInt);
        Assert.Null(item2.MissingField);
    }

    [Fact]
    public void MissingColumns()
    {
        using var reader = Sep.Reader().FromText("Name,Date,Enum,NullableInt\nJohn Doe,2023-01-15,A,42\nJane,2023-02-20,B,123");
        try
        {
            MyRecord.Parse(reader, CancellationToken.None).ToList();
            Assert.Fail("should throw");
        }
        catch (ArgumentException ex)
        {
            Assert.Equal("Missing required column 'ID' for required property 'Id'.", ex.Message);
        }
    }

    [Fact]
    public void CancelEnumeration()
    {
        using var reader = Sep.Reader().FromText(CSV_CONTENT);
        using var cts = new CancellationTokenSource();
        IEnumerable<MyRecord> enumerable = MyRecord.Parse(reader, cts.Token);
        IEnumerator<MyRecord> enumerator = enumerable.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        cts.Cancel();
        try
        {
            enumerator.MoveNext();
            Assert.Fail("Expected exception");
        }
        catch (OperationCanceledException ex)
        {
            Assert.Equal(cts.Token, ex.CancellationToken);
        }
        enumerator.Dispose();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CancelEnumerationAsync(bool tokenInParseAsync, bool tokenInGetAsyncEnumerator)
    {
        using var reader = Sep.Reader().FromText(CSV_CONTENT);
        using var cts = new CancellationTokenSource();
        IAsyncEnumerable<MyRecord> enumerable = MyRecord.ParseAsync(reader, tokenInParseAsync ? cts.Token : default);
        IAsyncEnumerator<MyRecord> enumerator = enumerable.GetAsyncEnumerator(tokenInGetAsyncEnumerator ? cts.Token : default);
        Assert.True(await enumerator.MoveNextAsync());
        cts.Cancel();
        try
        {
            await enumerator.MoveNextAsync();
            Assert.Fail("Expected exception");
        }
        catch (OperationCanceledException ex)
        {
            Assert.Equal(cts.Token, ex.CancellationToken);
        }
        await enumerator.DisposeAsync();
    }

    public partial class MyRecordWithNewDates
    {
        [CsvHeaderName("ID")]
        public int Id { get; set; }

        [CsvHeaderName("Date")]
        [CsvDateFormat("yyyy-MM-dd")]
        public DateOnly Date { get; set; }

        [CsvHeaderName("Time")]
        [CsvDateFormat("HH:mm:ss.fff")]
        public TimeOnly Time { get; set; }

        [CsvHeaderName("Offset")]
        [CsvDateFormat("o")]
        public DateTimeOffset Dto { get; set; }

        [GenerateCsvParser]
        public static partial IEnumerable<MyRecordWithNewDates> Parse(SepReader reader);
    }

    [Fact]
    public void ParseNewDates()
    {
        using var reader = Sep.Reader().FromText("ID,Date,Time,Offset\n1,2024-01-01,13:14:15.123,2024-05-10T10:00:00.0000000-05:00");
        var list = MyRecordWithNewDates.Parse(reader).ToList();
        var item1 = Assert.Single(list);

        Assert.Equal(1, item1.Id);
        Assert.Equal(new DateOnly(2024, 1, 1), item1.Date);
        Assert.Equal(new TimeOnly(13, 14, 15, 123), item1.Time);
        Assert.Equal(new DateTimeOffset(2024, 5, 10, 10, 0, 0, TimeSpan.FromHours(-5)), item1.Dto);
    }

    public partial class MyRecordWithAliasedColumns
    {
        [CsvHeaderName("a", "b")]
        public required int Value { get; init; }

        [GenerateCsvParser]
        public static partial IEnumerable<MyRecordWithAliasedColumns> Parse(SepReader reader);
    }

    [Theory]
    [InlineData("a\n1")]
    [InlineData("b\n1")]
    public void ParseAliasedColumns(string fileContents)
    {
        using var reader = Sep.Reader().FromText(fileContents);
        var list = MyRecordWithAliasedColumns.Parse(reader).ToList();
        var item1 = Assert.Single(list);
        Assert.Equal(1, item1.Value);
    }

    [Fact]
    public void MissingAliasedColumns()
    {
        using var reader = Sep.Reader().FromText("whoops,1");
        try
        {
            MyRecordWithAliasedColumns.Parse(reader).ToList();
            Assert.Fail("should throw");
        }
        catch (ArgumentException ex)
        {
            Assert.Equal("Missing required column with any of the following names: 'a', 'b' for required property 'Value'.", ex.Message);
        }
    }
}
