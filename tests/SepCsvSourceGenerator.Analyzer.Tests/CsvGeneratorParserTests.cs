using System.Reflection;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using nietras.SeparatedValues;

namespace SepCsvSourceGenerator.Analyzer.Tests
{
    public class CsvGeneratorParserTests
    {
        [Fact]
        public async Task ValidCase()
        {
            var diagnostics = await RunGenerator(@"
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
        public async Task MethodNotPartial()
        {
            var diagnostics = await RunGenerator(@"
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
        public async Task MethodNotStatic()
        {
            var diagnostics = await RunGenerator(@"
                public partial class MyRecord
                {
                    [GenerateCsvParser]
                    public partial IAsyncEnumerable<MyRecord> Parse(SepReader reader, CancellationToken cancellationToken);
                }
            ");

            Assert.Single(diagnostics);
            Assert.Equal("CSVGEN001", diagnostics[0].Id);
        }

        [Fact]
        public async Task InvalidReturnType()
        {
            var diagnostics = await RunGenerator(@"
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
        public async Task InvalidReturnTypeArgument()
        {
            var diagnostics = await RunGenerator(@"
                public partial class MyRecord
                {
                }
                public partial class AnotherRecord
                {
                    [GenerateCsvParser]
                    public static partial IAsyncEnumerable<MyRecord> Parse(SepReader reader, CancellationToken cancellationToken);
                }
            ");

            Assert.Single(diagnostics);
            Assert.Equal("CSVGEN002", diagnostics[0].Id);
        }

        [Fact]
        public async Task InvalidParameters()
        {
            var diagnostics = await RunGenerator(@"
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
        public async Task MissingDateFormat()
        {
            var diagnostics = await RunGenerator(@"
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

        private static Task<IReadOnlyList<Diagnostic>> RunGenerator(
            string code,
            bool wrap = true,
            CancellationToken cancellationToken = default)
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

            var refs = new[]
            {
                typeof(GenerateCsvParserAttribute).Assembly,
                typeof(SepReader).Assembly,
                typeof(IAsyncEnumerable<>).Assembly,
            };

            (ImmutableArray<Diagnostic> d, _) = RoslynTestUtils.RunGenerator(
                new CsvGenerator(),
                refs,
                new[] { text },
                cancellationToken: cancellationToken);

            return Task.FromResult<IReadOnlyList<Diagnostic>>(d);
        }
    }
}
