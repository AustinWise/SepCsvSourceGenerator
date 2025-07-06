
using nietras.SeparatedValues;

namespace US.AWise.SepCsvSourceGenerator.Analyzer.Tests
{
    public class CsvGeneratorEmitterTests
    {
        private static readonly bool s_generateBaselines = Environment.GetEnvironmentVariable("GENERATE_BASELINES")?.ToLowerInvariant() is "true" or "1";

        [Fact]
        public void Emitter_GeneratesCorrectCode_ForEnumProperty()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using US.AWise.SepCsvSourceGenerator;
using nietras.SeparatedValues;

namespace Test
{
    public enum MyEnum { A, B, C }
    public partial class MyRecord
    {
        [CsvHeaderName(""Status"")]
        public MyEnum Status { get; set; }

        [GenerateCsvParser]
        public static partial IAsyncEnumerable<MyRecord> ParseRecords(SepReader reader, CancellationToken cancellationToken);
    }
}
";
            RunTestAsync(source, "EnumProperty.generated.txt");
        }

        [Fact]
        public void Emitter_GeneratesCorrectCode_ForDateTimeProperty()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using US.AWise.SepCsvSourceGenerator;
using nietras.SeparatedValues;

namespace Test
{
    public partial class MyRecord
    {
        [CsvHeaderName(""Date"")]
        [CsvDateFormat(""yyyy-MM-dd"")]
        public DateTime Date { get; set; }

        [GenerateCsvParser]
        public static partial IAsyncEnumerable<MyRecord> ParseRecords(SepReader reader, CancellationToken cancellationToken);
    }
}
";
            RunTestAsync(source, "DateTimeProperty.generated.txt");
        }

        [Fact]
        public void Emitter_GeneratesCorrectCode_ForPropertyInBaseClass()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using US.AWise.SepCsvSourceGenerator;
using nietras.SeparatedValues;

namespace Test
{
    public class BaseRecord
    {
        [CsvHeaderName(""BaseName"")]
        public required string BaseName { get; init; }
    }
    public partial class DerivedRecord : BaseRecord
    {
        [CsvHeaderName(""DerivedName"")]
        public required string DerivedName { get; init; }

        [GenerateCsvParser]
        public static partial IAsyncEnumerable<DerivedRecord> ParseRecords(SepReader reader, CancellationToken cancellationToken);
    }
}
";
            RunTestAsync(source, "PropertyInBaseClass.generated.txt");
        }

        [Fact]
        public void Emitter_GeneratesCorrectCode_ForClassInNamespace()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using US.AWise.SepCsvSourceGenerator;
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
            RunTestAsync(source, "ClassInNamespace.generated.txt");
        }

        [Fact]
        public void Emitter_GeneratesCorrectCode_ForRequiredProperty()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using US.AWise.SepCsvSourceGenerator;
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
            RunTestAsync(source, "RequiredProperty.generated.txt");
        }

        [Fact]
        public void Emitter_GeneratesCorrectCode_ForNestedClass()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using US.AWise.SepCsvSourceGenerator;
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
            RunTestAsync(source, "NestedClass.generated.txt");
        }

        [Fact]
        public void Emitter_GeneratesCorrectCode_ForRecordStruct()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using US.AWise.SepCsvSourceGenerator;
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
            RunTestAsync(source, "RecordStruct.generated.txt");
        }

        [Fact]
        public void Emitter_GeneratesCorrectCode_ForGlobalNamespaceClass()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using US.AWise.SepCsvSourceGenerator;
using nietras.SeparatedValues;

public partial class MyGlobalRecord
{
    [CsvHeaderName(""Name"")]
    public string Name { get; set; }

    [GenerateCsvParser]
    public static partial IAsyncEnumerable<MyGlobalRecord> ParseRecords(SepReader reader, CancellationToken cancellationToken);
}
";
            RunTestAsync(source, "GlobalNamespaceClass.generated.txt");
        }

        [Fact]
        public void Emitter_GeneratesCorrectCode_ForIEnumerable()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using US.AWise.SepCsvSourceGenerator;
using nietras.SeparatedValues;

public partial class MyGlobalRecord
{
    [CsvHeaderName(""Name"")]
    public string Name { get; set; }

    public string UnrelatedProperty { get; set; }

    [GenerateCsvParser]
    public static partial IEnumerable<MyGlobalRecord> ParseRecords(SepReader reader, CancellationToken cancellationToken);
}
";
            RunTestAsync(source, "IEnumerable.generated.txt");
        }

        [Fact]
        public void Emitter_GeneratesCorrectCode_ForDifferentModifiers()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using US.AWise.SepCsvSourceGenerator;
using nietras.SeparatedValues;

public abstract class MyBaseClass
{
    protected abstract IAsyncEnumerable<MyGlobalRecord> ParseRecords(SepReader reader, CancellationToken cancellationToken);
}

public partial class MyGlobalRecord : MyBaseClass
{
    [CsvHeaderName(""Name"")]
    public string Name { get; set; }

    [GenerateCsvParser]
    protected override partial IAsyncEnumerable<MyGlobalRecord> ParseRecords(SepReader reader, CancellationToken cancellationToken);
}
";
            RunTestAsync(source, "DifferentModifiers.generated.txt");
        }

        [Fact]
        public void Emitter_GeneratesCorrectCode_ForGenericClass()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using US.AWise.SepCsvSourceGenerator;
using nietras.SeparatedValues;

namespace Test
{
    public partial class MyGenericRecord<T> where T : ISpanParsable<T>
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
            RunTestAsync(source, "GenericClass.generated.txt");
        }

        private static void RunTestAsync(string source, string baselineFileName)
        {
            var refs = new[]
            {
                typeof(SepReader).Assembly,
                typeof(IAsyncEnumerable<>).Assembly,
            };

            var (diagnostics, generatedSources) = RoslynTestUtils.RunGenerator(
                new CsvGenerator(),
                refs,
                [source]);

            Assert.Empty(diagnostics);

            var generatedParser = generatedSources.Single(s => s.HintName.EndsWith(".CsvGenerator.g.cs"));

            if (s_generateBaselines)
            {
                // Walk up the directory tree until we find the Baselines directory.
                string? directory = Path.GetDirectoryName(Path.GetDirectoryName(Environment.ProcessPath));
                while (directory != null && !Directory.Exists(Path.Combine(directory, "Baselines")))
                {
                    directory = Path.GetDirectoryName(directory)!;
                }
                if (directory == null)
                {
                    throw new Exception("Could not find the Baselines directory.");
                }
                File.WriteAllText(Path.Combine(directory, "Baselines", baselineFileName), generatedParser.Source);
            }
            else
            {
                var baseline = File.ReadAllText(Path.Combine("Baselines", baselineFileName));

                var expected = baseline.ReplaceLineEndings();
                var actual = generatedParser.Source.ReplaceLineEndings();

                Assert.Equal(expected, actual);
            }

        }
    }
}
