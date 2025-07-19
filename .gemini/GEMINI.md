# SepCsvSourceGenerator

This is a Roslyn Incremental Source Generator. It generates code to parse CSV files using the Sep
CSV parsing library.

When changing this library, update the unit tests as appropriate:

* CsvGeneratorParserTests - when adding or changing diagnostics
* CsvGeneratorEmitterTests - changing the code that is generated
* RunGeneratedParserTests - changing how the generated code behaves

## Tools

To test changes:

```cmd
dotnet test
```

When editing C# (aka csharp, .cs) files or adding new diagnostics, use the following commands to fix warnings and apply formatting:

```cmd
dotnet format --severity info
```

When update the unit tests in `CsvGeneratorEmitterTests.cs`, you can update the baseline files by
running the following command:

```cmd
update-test-baselines.cmd
```
