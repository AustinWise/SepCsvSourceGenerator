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
    // 
    // [GenerateCsvParser]
    //public static partial IAsyncEnumerable<MyClass> ParseAsync(SepReader reader, CancellationToken ct = default);

    [CsvHeaderName("Transaction Date")]
    [CsvDateFormat("MM/dd/yyyy")]
    public required DateTime TransactionDate { get; init; }

    public string? SomethingElse { get; set; }

    // This is what the source generator will generator:

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
}
