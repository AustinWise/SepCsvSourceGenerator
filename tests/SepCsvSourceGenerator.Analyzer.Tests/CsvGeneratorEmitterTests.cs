using nietras.SeparatedValues;

namespace SepCsvSourceGenerator.Analyzer.Tests
{
    public class CsvGeneratorEmitterTests
    {
        private static readonly bool s_generateBaselines = Environment.GetEnvironmentVariable("GENERATE_BASELINES")?.ToLowerInvariant() is "true" or "1";

        [Fact]
        public async Task Emitter_GeneratesCorrectCode_ForBasicClass()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using SepCsvSourceGenerator;
using nietras.SeparatedValues;

namespace Test
{
    public partial class MyRecord
    {
        [CsvHeaderName(""Name"")]
        public string Name { get; set; }

        [CsvHeaderName(""Date"")]
        [CsvDateFormat(""yyyy-MM-dd"")]
        public DateTime Date { get; set; }

        [CsvHeaderName(""Value"")]
        public int Value { get; set; }

        [GenerateCsvParser]
        public static partial IAsyncEnumerable<MyRecord> ParseRecords(SepReader reader, CancellationToken cancellationToken);
    }
}
";
            await RunTestAsync(source, "BasicClass.generated.txt");
        }

        [Fact]
        public async Task Emitter_GeneratesCorrectCode_ForClassInNamespace()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using SepCsvSourceGenerator;
using nietras.SeparatedValues;

namespace My.Awesome.Namespace
{
    public partial class MyRecordInNamespace
    {
        [CsvHeaderName(""Name"")]
        public string Name { get; set; }

        [GenerateCsvParser]
        public static partial IAsyncEnumerable<MyRecordInNamespace> ParseRecords(SepReader reader, CancellationToken cancellationToken);
    }
}
";
            await RunTestAsync(source, "ClassInNamespace.generated.txt");
        }

        [Fact]
        public async Task Emitter_GeneratesCorrectCode_ForRequiredProperty()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using SepCsvSourceGenerator;
using nietras.SeparatedValues;

namespace Test
{
    public partial class MyRecordWithRequired
    {
        [CsvHeaderName(""Name"")]
        public required string Name { get; set; }

        [GenerateCsvParser]
        public static partial IAsyncEnumerable<MyRecordWithRequired> ParseRecords(SepReader reader, CancellationToken cancellationToken);
    }
}
";
            await RunTestAsync(source, "RequiredProperty.generated.txt");
        }

        [Fact]
        public async Task Emitter_GeneratesCorrectCode_ForIgnoredProperty()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using SepCsvSourceGenerator;
using nietras.SeparatedValues;

namespace Test
{
    public partial class MyRecordWithIgnored
    {
        [CsvHeaderName(""Name"")]
        public string Name { get; set; }

        [CsvIgnore]
        public int ShouldBeIgnored { get; set; }

        [GenerateCsvParser]
        public static partial IAsyncEnumerable<MyRecordWithIgnored> ParseRecords(SepReader reader, CancellationToken cancellationToken);
    }
}
";
            await RunTestAsync(source, "IgnoredProperty.generated.txt");
        }

        [Fact]
        public async Task Emitter_GeneratesCorrectCode_ForNestedClass()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using SepCsvSourceGenerator;
using nietras.SeparatedValues;

namespace Test
{
    public partial class OuterClass
    {
        public partial class NestedClass
        {
            [CsvHeaderName(""Name"")]
            public string Name { get; set; }

            [GenerateCsvParser]
            public static partial IAsyncEnumerable<NestedClass> ParseRecords(SepReader reader, CancellationToken cancellationToken);
        }
    }
}
";
            await RunTestAsync(source, "NestedClass.generated.txt");
        }

        [Fact]
        public async Task Emitter_GeneratesCorrectCode_ForRecordStruct()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using SepCsvSourceGenerator;
using nietras.SeparatedValues;

namespace Test
{
    public partial record struct MyRecordStruct([property: CsvHeaderName(""Name"")] string Name)
    {
        [GenerateCsvParser]
        public static partial IAsyncEnumerable<MyRecordStruct> ParseRecords(SepReader reader, CancellationToken cancellationToken);
    }
}
";
            await RunTestAsync(source, "RecordStruct.generated.txt");
        }

        [Fact]
        public async Task Emitter_GeneratesCorrectCode_ForGlobalNamespaceClass()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using SepCsvSourceGenerator;
using nietras.SeparatedValues;

public partial class MyGlobalRecord
{
    [CsvHeaderName(""Name"")]
    public string Name { get; set; }

    [GenerateCsvParser]
    public static partial IAsyncEnumerable<MyGlobalRecord> ParseRecords(SepReader reader, CancellationToken cancellationToken);
}
";
            await RunTestAsync(source, "GlobalNamespaceClass.generated.txt");
        }

        [Fact]
        public async Task Emitter_GeneratesCorrectCode_ForGenericClass()
        {
            // TODO: make this only work when T is constrained to implement ISpanParsable.
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using SepCsvSourceGenerator;
using nietras.SeparatedValues;

namespace Test
{
    public partial class MyGenericRecord<T> where T : new()
    {
        [CsvHeaderName(""Name"")]
        public string Name { get; set; }

        [CsvHeaderName(""Value"")]
        public T Value { get; set; }

        [GenerateCsvParser]
        public static partial IAsyncEnumerable<MyGenericRecord<T>> ParseRecords(SepReader reader, CancellationToken cancellationToken);
    }
}
";
            await RunTestAsync(source, "GenericClass.generated.txt");
        }

        private static async Task RunTestAsync(string source, string baselineFileName)
        {
            var refs = new[]
            {
                typeof(GenerateCsvParserAttribute).Assembly,
                typeof(SepReader).Assembly,
                typeof(IAsyncEnumerable<>).Assembly,
            };

            var (diagnostics, generatedSources) = RoslynTestUtils.RunGenerator(
                new CsvGenerator(),
                refs,
                [source]);

            Assert.Empty(diagnostics);
            Assert.Single(generatedSources);

            if (s_generateBaselines)
            {
                // Wall up the directory tree until we find the Baselines directory.
                string? directory = Path.GetDirectoryName(Path.GetDirectoryName(Environment.ProcessPath));
                while (directory != null && !Directory.Exists(Path.Combine(directory, "Baselines")))
                {
                    directory = Path.GetDirectoryName(directory)!;
                }
                if (directory == null)
                {
                    throw new Exception("Could not find the Baselines directory.");
                }
                await File.WriteAllTextAsync(Path.Combine(directory, "Baselines", baselineFileName), generatedSources[0].Source);
            }
            else
            {
                var baseline = await File.ReadAllTextAsync(Path.Combine("Baselines", baselineFileName));

                var expected = baseline.ReplaceLineEndings();
                var actual = generatedSources[0].Source.ReplaceLineEndings();

                Assert.Equal(expected, actual);
            }

        }
    }
}
