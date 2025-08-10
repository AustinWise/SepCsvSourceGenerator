using Microsoft.CodeAnalysis;
using nietras.SeparatedValues;
using System.Collections.Immutable;
using System.Reflection;

namespace AWise.SepCsvSourceGenerator.Analyzer.Tests
{
    public class CsvGeneratorParserTests
    {
        [Fact]
        public void ValidStaticCase()
        {
            var diagnostics = RunGenerator(@"
                public partial class MyRecord
                {
                    [CsvHeaderName(""Name"")]
                    public string? Name { get; set; }

                    [CsvHeaderName(""Date"")]
                    [CsvDateFormat(""yyyy-MM-dd"")]
                    public DateTime Date { get; set; }

                    [GenerateCsvParser]
                    public static partial IAsyncEnumerable<MyRecord> Parse(SepReader reader, CancellationToken cancellationToken);
                }
            ");

            Assert.Empty(diagnostics);
        }

        [Fact]
        public void ValidInstanceCase()
        {
            var diagnostics = RunGenerator(@"
                public partial class MyRecord
                {
                    [CsvHeaderName(""Name"")]
                    public string? Name { get; set; }

                    [CsvHeaderName(""Date"")]
                    [CsvDateFormat(""yyyy-MM-dd"")]
                    public DateTime Date { get; set; }

                    [GenerateCsvParser]
                    public partial IAsyncEnumerable<MyRecord> Parse(SepReader reader, CancellationToken cancellationToken);
                }
            ");

            Assert.Empty(diagnostics);
        }

        [Fact]
        public void ValidMultipleHeaderNames()
        {
            var diagnostics = RunGenerator(@"
                public partial class MyRecord
                {
                    [CsvHeaderName(""a"", ""b"")]
                    public string? Name { get; set; }

                    [GenerateCsvParser]
                    public partial IAsyncEnumerable<MyRecord> Parse(SepReader reader, CancellationToken cancellationToken);
                }
            ");

            Assert.Empty(diagnostics);
        }

        [Fact]
        public void MissingHeaderNames()
        {
            var diagnostics = RunGenerator(@"
                public partial class MyRecord
                {
                    [CsvHeaderName]
                    public string? Name { get; set; }

                    [GenerateCsvParser]
                    public partial IAsyncEnumerable<MyRecord> Parse(SepReader reader, CancellationToken cancellationToken);
                }
            ");

            var diag = Assert.Single(diagnostics);
            Assert.Equal("CSVGEN009", diag.Id);
        }

        [Fact]
        public void InitNonNullableNonRequiredPropertyGeneratesWarning()
        {
            var diagnostics = RunGenerator(@"
                public partial class MyRecord
                {
                    [CsvHeaderName(""Name"")]
                    public string Name { get; init; }

                    [GenerateCsvParser]
                    public partial IAsyncEnumerable<MyRecord> Parse(SepReader reader, CancellationToken cancellationToken);
                }
            ");

            // We let the compile generate a warning here, since it's error message tells the user how to resolve the problem.
            Assert.Equal(2, diagnostics.Length);
            // Possible null reference assignment.
            Assert.True(diagnostics.Any(d => d.Id == "CS8601"));
            // Non-nullable property 'Name' must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring the property as nullable.
            Assert.True(diagnostics.Any(d => d.Id == "CS8618"));
        }

        [Fact]
        public void MethodNotPartial()
        {
            var diagnostics = RunGenerator(@"
                public partial class MyRecord
                {
                    [GenerateCsvParser]
                    public static IAsyncEnumerable<MyRecord> Parse(SepReader reader, CancellationToken cancellationToken) => throw new Exception();
                }
            ");

            Assert.Single(diagnostics);
            Assert.Equal("CSVGEN001", diagnostics[0].Id);
        }

        [Fact]
        public void InvalidReturnType()
        {
            var diagnostics = RunGenerator(@"
                public partial class MyRecord
                {
                    [GenerateCsvParser]
                    public static partial void Parse(SepReader reader, CancellationToken cancellationToken);
                }
            ");

            Assert.True(diagnostics.Any(d => d.Id == "CSVGEN002"));
        }

        [Fact]
        public void InvalidReturnTypeArgument()
        {
            var diagnostics = RunGenerator(@"
                public partial class MyRecord
                {
                    [GenerateCsvParser]
                    public static partial List<MyRecord> Parse(SepReader reader, CancellationToken cancellationToken);
                }
            ");

            Assert.True(diagnostics.Any(d => d.Id == "CSVGEN002"));
        }

        [Fact]
        public void UnexpectedParameterType()
        {
            var diagnostics = RunGenerator(@"
                public partial class MyRecord
                {
                    [CsvHeaderName(""Name"")]
                    public string? Name { get; set; }

                    [GenerateCsvParser]
                    public static partial IEnumerable<MyRecord> Parse(string unexpected, SepReader reader);
                }
            ");

            var d = Assert.Single(diagnostics);
            Assert.Equal("CSVGEN010", d.Id);
        }

        [Fact]
        public void NoPropertiesFound()
        {
            var diagnostics = RunGenerator(@"
                public partial class MyRecord
                {
                    [GenerateCsvParser]
                    public static partial IEnumerable<MyRecord> Parse(SepReader reader, CancellationToken cancellationToken);
                }
            ");

            Assert.Single(diagnostics);
            Assert.Equal("CSVGEN007", diagnostics[0].Id);
        }

        [Fact]
        public void MissingParameters()
        {
            var diagnostics = RunGenerator(@"
                public partial class MyRecord
                {
                    [GenerateCsvParser]
                    public static partial IAsyncEnumerable<MyRecord> Parse();
                }
            ");

            var d = Assert.Single(diagnostics);
            Assert.Equal("CSVGEN011", d.Id);
        }

        [Fact]
        public void DuplicateCancellationTokenParameter()
        {
            var diagnostics = RunGenerator(@"
                public partial class MyRecord
                {
                    [GenerateCsvParser]
                    public static partial IAsyncEnumerable<MyRecord> Parse(SepReader reader, CancellationToken ct1, CancellationToken ct2);
                }
            ");

            var d = Assert.Single(diagnostics);
            Assert.Equal("CSVGEN013", d.Id);
        }

        [Fact]
        public void DuplicateHeaderParameter()
        {
            var diagnostics = RunGenerator(@"
                public partial class MyRecord
                {
                    [GenerateCsvParser]
                    public static partial IEnumerable<MyRecord> Parse(SepReaderHeader header1, SepReaderHeader header2, IEnumerable<SepReader.Row> records);
                }
            ");

            var d = Assert.Single(diagnostics);
            Assert.Equal("CSVGEN014", d.Id);
        }

        [Fact]
        public void DuplicateReaderParameter()
        {
            var diagnostics = RunGenerator(@"
                public partial class MyRecord
                {
                    [GenerateCsvParser]
                    public static partial IAsyncEnumerable<MyRecord> Parse(SepReader reader, SepReader reader2);
                }
            ");

            var d = Assert.Single(diagnostics);
            Assert.Equal("CSVGEN015", d.Id);
        }

        [Fact]
        public void MissingHeaderParameter()
        {
            var diagnostics = RunGenerator(@"
                public partial class MyRecord
                {
                    [GenerateCsvParser]
                    public static partial IEnumerable<MyRecord> Parse(IEnumerable<SepReader.Row> records);
                }
            ");

            var d = Assert.Single(diagnostics);
            Assert.Equal("CSVGEN012", d.Id);
        }

        [Fact]
        public void MismatchIAsyncEnumerable()
        {
            var diagnostics = RunGenerator(@"
                public partial class MyRecord
                {
                    [GenerateCsvParser]
                    public static partial IAsyncEnumerable<MyRecord> Parse(SepReaderHeader header, IEnumerable<SepReader.Row> records);
                }
            ");

            var d = Assert.Single(diagnostics);
            Assert.Equal("CSVGEN016", d.Id);
        }

        [Fact]
        public void MismatchIEnumerable()
        {
            var diagnostics = RunGenerator(@"
                public partial class MyRecord
                {
                    [GenerateCsvParser]
                    public static partial IEnumerable<MyRecord> Parse(SepReaderHeader header, IAsyncEnumerable<SepReader.Row> records);
                }
            ");

            var d = Assert.Single(diagnostics);
            Assert.Equal("CSVGEN017", d.Id);
        }

        [Fact]
        public void MissingDateFormat()
        {
            var diagnostics = RunGenerator(@"
                public partial class MyRecord
                {
                    [CsvHeaderName(""Date"")]
                    public DateTime Date { get; set; }

                    [GenerateCsvParser]
                    public static partial IAsyncEnumerable<MyRecord> Parse(SepReader reader, CancellationToken cancellationToken);
                }
            ");

            Assert.Single(diagnostics);
            Assert.Equal("CSVGEN004", diagnostics[0].Id);
        }

        [Fact]
        public void InvalidHeaderName()
        {
            var diagnostics = RunGenerator(@"
                public partial class MyRecord
                {
                    [CsvHeaderName(""Valid"")]
                    public string? Valid { get; set; }

                    [CsvHeaderName("""")]
                    public string? Invalid { get; set; }

                    [GenerateCsvParser]
                    public static partial IAsyncEnumerable<MyRecord> Parse(SepReader reader, CancellationToken cancellationToken);
                }
            ");

            Assert.Single(diagnostics);
            Assert.Equal("CSVGEN006", diagnostics[0].Id);
        }

        [Fact]
        public void EssentialTypesNotFound()
        {
            var diagnostics = RunGenerator(@"
                public partial class MyRecord
                {
                    [CsvHeaderName(""Value"")]
                    public string Value { get; set; }

                    [GenerateCsvParser]
                    public static partial IAsyncEnumerable<MyRecord> Parse(SepReader reader, CancellationToken cancellationToken);
                }
            ", includeSepRef: false);


            var diag = diagnostics.Where(d => d.Id == "CSVGEN005").Single();
            Assert.Equal("Essential types for source generation were not found: SepReader, SepReader.Row, SepReaderHeader", diag.GetMessage());
        }

        [Fact]
        public void PropertyNotParsable()
        {
            var diagnostics = RunGenerator(@"
                public class NotParsable { }

                public partial class MyRecord
                {
                    [CsvHeaderName(""Value"")]
                    public NotParsable Value { get; set; }

                    [GenerateCsvParser]
                    public static partial IAsyncEnumerable<MyRecord> Parse(SepReader reader, CancellationToken cancellationToken);
                }
            ");

            Assert.True(diagnostics.Any(d => d.Id == "CSVGEN008"));
        }

        private static ImmutableArray<Diagnostic> RunGenerator(
            string code,
            bool wrap = true,
            bool includeSepRef = true)
        {
            code = "#nullable enable\n" + code;
            var text = code;
            if (wrap)
            {
                text = $@"
using System;
using System.Collections.Generic;
using System.Threading;
using AWise.SepCsvSourceGenerator;
using nietras.SeparatedValues;

namespace Test
{{
    {code}
}}
";
            }

            Assembly[]? refs;
            if (includeSepRef)
            {
                refs =
                [
                    typeof(SepReader).Assembly,
                    typeof(IAsyncEnumerable<>).Assembly,
                ];
            }
            else
            {
                refs =
                [
                    typeof(IAsyncEnumerable<>).Assembly,
                ];
            }

            var compilation = RoslynTestUtils.CreateCompilation([text], refs);
            var (_, diagnostics) = RoslynTestUtils.RunGenerator(compilation, new CsvGenerator());

            // CS8795: Partial method 'MyRecord.Parse(string, SepReader)' must have an implementation part because it has accessibility modifiers.
            // We disable this error, because basically every negative case will have this error.
            return [.. diagnostics.Where(d => d.Id != "CS8795")];
        }
    }
}
