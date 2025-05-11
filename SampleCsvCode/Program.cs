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
    //public static partial IAsyncEnumerable<MyClass> ParseAsync(SepReader reader, CancellationToken ct = default);

    //public async static partial IAsyncEnumerable<MyClass> ParseAsync(SepReader reader, [EnumeratorCancellation] CancellationToken ct)

    public async static IAsyncEnumerable<MyClass> ParseAsync(SepReader reader, [EnumeratorCancellation] CancellationToken ct = default)
    {
        int transactionDateNdx;
        int somethingElseNdx;

        // required member
        if (!reader.Header.TryIndexOf("Transaction Date", out transactionDateNdx))
        {
            throw new ArgumentException("Missing column name 'Transaction Date'");
        }

        // not required member
        if (!reader.Header.TryIndexOf("SomethingElse", out somethingElseNdx))
        {
            somethingElseNdx = -1;
        }

        await foreach (SepReader.Row row in reader)
        {
            ct.ThrowIfCancellationRequested();

            MyClass ret = new MyClass()
            {
                TransactionDate = DateTime.ParseExact(row[transactionDateNdx].Span, "MM/dd/yyyy", CultureInfo.InvariantCulture),
            };
            if (somethingElseNdx != -1)
            {
                ret.SomethingElse = row[somethingElseNdx].Span.ToString();
            }
            yield return ret;
        }
    }

    // Transaction Date,Post Date,Description,Category,Type,Amount,Memo

    [CsvHeaderName("Transaction Date")]
    [CsvDateFormat("MM/dd/yyyy")]
    public required DateTime TransactionDate { get; init; }


    //[CsvHeaderName("Post Date")]
    //public string PostDate { get; init; }

    //public string Description { get; init; }

    //public string Category { get; set; }

    //public string Type { get; set; }

    //public string Amount { get; set; }

    //public string Memo { get; set; }

    public string? SomethingElse { get; set; }
}
