using nietras.SeparatedValues;
using SepCsvSourceGenerator;
using System.Globalization;
using System.Runtime.CompilerServices;

using var reader = await Sep.Reader().FromFileAsync(args[0]);

await foreach (var my in MyClass.ParseAsync(reader))
{
    Console.WriteLine(my.TransactionDate);
}

partial class MyClass
{
    // This is what the user would declare
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

    // This is what the source generator will generator:

    public async static partial IAsyncEnumerable<MyClass> ParseAsync(SepReader reader, [EnumeratorCancellation] CancellationToken ct)
    {

        // required member
        if (!reader.Header.TryIndexOf("Transaction Date", out int transactionDateNdx))
        {
            throw new ArgumentException("Missing column name 'Transaction Date'");
        }
        if (!reader.Header.TryIndexOf("Amount", out int amountNdx))
        {
            throw new ArgumentException("Missing column name 'Amount'");
        }
        if (!reader.Header.TryIndexOf("Name", out int nameNdx))
        {
            throw new ArgumentException("Missing column name 'Name'");
        }

        // not required member
        if (!reader.Header.TryIndexOf("Duration", out int durationNdx))
        {
            durationNdx = -1;
        }

        await foreach (SepReader.Row row in reader)
        {
            ct.ThrowIfCancellationRequested();

            MyClass ret = new()
            {
                // DateTime optionally supports a format string.
                TransactionDate = DateTime.ParseExact(row[transactionDateNdx].Span, "MM/dd/yyyy", CultureInfo.InvariantCulture),
                // types that implement ISpanParsable<T> are parsed this way
                Amount = int.Parse(row[amountNdx].Span, CultureInfo.InvariantCulture),
                // while string implements ISpanParsable<string>, it is special cased to directly call ToString() on the Span.
                Name = row[nameNdx].Span.ToString(),
            };
            if (durationNdx != -1)
            {
                // types that implement ISpanParsable<T> are parsed this way
                ret.Duration = float.Parse(row[durationNdx].Span, CultureInfo.InvariantCulture);
            }
            yield return ret;
        }
    }
}
