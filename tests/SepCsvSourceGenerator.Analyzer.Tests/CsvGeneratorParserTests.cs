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
        public void InvalidParameters()
        {
            var diagnostics = RunGenerator(@"
                public partial class MyRecord
                {
                    [GenerateCsvParser]
                    public static partial IAsyncEnumerable<MyRecord> Parse(SepReader reader);
                }
            ");

            Assert.True(diagnostics.Any(d => d.Id == "CSVGEN003"));
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
            Assert.Equal("Essential types for source generation were not found: SepReader", diag.GetMessage());
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

            return diagnostics;
        }
    }
}
