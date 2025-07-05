using Microsoft.CodeAnalysis;
using nietras.SeparatedValues;
using System.Collections.Immutable;
using System.Reflection;

namespace SepCsvSourceGenerator.Analyzer.Tests
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
                }
                public partial class AnotherClass
                {
                    [GenerateCsvParser]
                    public static partial IEnumerable<MyRecord> Parse(SepReader reader, CancellationToken cancellationToken);
                }
            ");

            Assert.Single(diagnostics);
            Assert.Equal("CSVGEN002", diagnostics[0].Id);
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
                    [CsvHeaderName("""")]
                    public string Name { get; set; }

                    [GenerateCsvParser]
                    public static partial IAsyncEnumerable<MyRecord> Parse(SepReader reader, CancellationToken cancellationToken);
                }
            ");

            Assert.Single(diagnostics);
            Assert.Equal("CSVGEN006", diagnostics[0].Id);
        }

        private static ImmutableArray<Diagnostic> RunGenerator(
            string code,
            bool wrap = true,
            bool includeRefs = true)
        {
            var text = code;
            if (wrap)
            {
                text = $@"
using System;
using System.Collections.Generic;
using System.Threading;
using SepCsvSourceGenerator;
using nietras.SeparatedValues;

namespace Test
{{
    {code}
}}
";
            }

            Assembly[]? refs;
            if (includeRefs)
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
            var (outputCompilation, diagnostics) = RoslynTestUtils.RunGenerator(compilation, new CsvGenerator());

            return diagnostics;
        }
    }
}
