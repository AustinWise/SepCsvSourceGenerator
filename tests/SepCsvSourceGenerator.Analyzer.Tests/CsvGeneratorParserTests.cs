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
                    public string Name { get; set; }

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
                    public string Name { get; set; }

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
        public void MethodNotPartial()
        {
            var diagnostics = RunGenerator(@"
                public partial class MyRecord
                {
                    [GenerateCsvParser]
                    public static IAsyncEnumerable<MyRecord> Parse(SepReader reader, CancellationToken cancellationToken) => default;
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

            Assert.Single(diagnostics);
            Assert.Equal("CSVGEN002", diagnostics[0].Id);
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

            Assert.Single(diagnostics);
            Assert.Equal("CSVGEN002", diagnostics[0].Id);
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

            Assert.Single(diagnostics);
            Assert.Equal("CSVGEN003", diagnostics[0].Id);
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
                    public string Valid { get; set; }

                    [CsvHeaderName("""")]
                    public string Invalid { get; set; }

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

            Assert.Single(diagnostics);
            Assert.Equal("CSVGEN005", diagnostics[0].Id);
            Assert.Equal("Essential types for source generation were not found: SepReader", diagnostics[0].GetMessage());
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

            Assert.Single(diagnostics);
            Assert.Equal("CSVGEN008", diagnostics[0].Id);
        }

        private static ImmutableArray<Diagnostic> RunGenerator(
            string code,
            bool wrap = true,
            bool includeSepRef = true)
        {
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
