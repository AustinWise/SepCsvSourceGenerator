# A source generator for Sep CSV parsing

This is a C# source generator for generating strongly typed parsing of CSV
files using the excellent [Sep CSV library](https://github.com/nietras/Sep).

## Usage

First, add a reference to the [Nuget packages](https://www.nuget.org/packages/AWise.SepCsvSourceGenerator/):

```sh
dotnet add package Sep
dotnet add package AWise.SepCsvSourceGenerator
```

Note that this source generator requires the .NET SDK 9.0.300 or higher, which is included with Visual Studio 2022 v17.14 or higher.

To use the source generator, define a partial class and a partial static method with the `[GenerateCsvParser]` attribute.
The source generator will generate the implementation of this method.

Here is an example:

```csharp
using nietras.SeparatedValues;
using AWise.SepCsvSourceGenerator;

public partial class MyRecord
{
    [CsvHeaderName("Name")]
    public required string Name { get; set; }

    [CsvHeaderName("Date")]
    [CsvDateFormat("yyyy-MM-dd")]
    public DateTime Date { get; set; }

    [GenerateCsvParser]
    public static partial IEnumerable<MyRecord> Parse(SepReader reader, CancellationToken ct);
}
```

You can then use the generated `Parse` method to parse a CSV file:

```csharp
const string CSV_CONTENT = "Name,Date\nJohn,2023-01-15\nJane,2023-02-20";

using var reader = Sep.Reader().FromText(CSV_CONTENT);
var records = MyRecord.Parse(reader, CancellationToken.None);

foreach (var record in records)
{
    Console.WriteLine($"Name: {record.Name}, Date: {record.Date.ToShortDateString()}");
}
```

If a property is is `init` or `required`, an exception will be thrown if the CSV file does not have
the column. If the property has `set`, the generated parser will only set the property if the column
exists in the CSV file.

The only types that are supported for parsing are enums and types that implement
[ISpanParsable](https://learn.microsoft.com/en-us/dotnet/api/system.ispanparsable-1).

`DateTime`, `DateTimeOffset`, `DateOnly`, and `TimeOnly` are given special treatment. They are parsed with
their respective `ParseExact` methods using `CultureInfo.InvariantCulture`. Specify the date-time format using the `CsvDateFormat` attribute.

### Supported attributes

The following attributes are supported on the properties of the partial class:

* `[CsvHeaderName("...")]`: Specifies the name of the column in the CSV file.
* `[CsvDateFormat("...")]`: Specifies the date format for `DateTime`, `DateTimeOffset`, `DateOnly`, and `TimeOnly` properties.

### Async parsing

The source generator also supports generating asynchronous parsing methods. To do this, define a method that returns an `IAsyncEnumerable<T>`:

```csharp
[GenerateCsvParser]
public static partial IAsyncEnumerable<MyRecord> ParseAsync(SepReader reader, CancellationToken ct);
```
