# A source generator for Sep CSV parsing

This is a C# source generator for generating strongly typed parsing of CSV
files using the excellent [Sep CSV library](https://github.com/nietras/Sep).

## Usage

First, add a reference to the Nuget packages:

```sh
dotnet add package Sep
dotnet add package AWise.SepCsvSourceGenerator
```

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

The only types that are supported for parsing are types that implement
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

## TODO

* Consider not requiring the `CsvHeaderName` attribute on every property. We could take inspiration
  from how `System.Text.Json` [does it](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/customize-properties).
* Support calling implementations of `ISpanParsable` that are explicitly implemented.
* Consider supporting something other than `DateTime.ParseExact` for date parsing. It is a little
  annoying to specify the format every time when we could potentially let `DateTime.Parse` "figure it out".
* Make the cancellation token optional. Note that you can declare a default value for the cancellation
  token already, which makes it optional for callers.
* Consider allowing the parameters to the method be named something other than `reader` and `ct`.
* Consider adding support for list-like types for properties (like arrays or List\<T\>).
* Figure out what to do with the `AnalyzerReleases.Shipped.md` and `AnalyzerReleases.Unshipped.md` files.
* Consider adding support for earlier Roslyn versions, to support older .NET SDKs and Visual Studio versions.
