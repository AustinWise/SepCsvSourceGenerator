using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Reflection;

namespace US.AWise.SepCsvSourceGenerator.Analyzer.Tests
{
    internal static class RoslynTestUtils
    {
        public static (ImmutableArray<Diagnostic>, ImmutableArray<GeneratedSourceResult>) RunGenerator(
            IIncrementalGenerator generator,
            Assembly[]? additionalReferences,
            string[] sources,
            LanguageVersion languageVersion = LanguageVersion.Default)
        {
            var compilation = CreateCompilation(sources, additionalReferences, languageVersion);
            var (outputCompilation, generatorDiagnostics) = RunGenerator(compilation, generator);
            var generatedSources = outputCompilation.SyntaxTrees
                .Where(t => !compilation.SyntaxTrees.Any(t2 => t2.FilePath == t.FilePath))
                .Select(t => new GeneratedSourceResult(
                    Path.GetFileName(t.FilePath),
                    t,
                    t.GetText().ToString()))
                .ToImmutableArray();

            var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
            diagnostics.AddRange(outputCompilation.GetDiagnostics().Where(d => d.DefaultSeverity >= DiagnosticSeverity.Warning));
            diagnostics.AddRange(generatorDiagnostics);

            return (diagnostics.ToImmutableArray(), generatedSources);
        }

        public static (Compilation, ImmutableArray<Diagnostic>) RunGenerator(Compilation compilation, IIncrementalGenerator generator)
        {
            var driver = CSharpGeneratorDriver.Create(generator);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics, CancellationToken.None);
            return (outputCompilation, diagnostics);
        }

        public static Compilation CreateCompilation(
            string[] sources,
            Assembly[]? additionalReferences = null,
            LanguageVersion languageVersion = LanguageVersion.Default)
        {
            var syntaxTrees = sources.Select(s => CSharpSyntaxTree.ParseText(s, new CSharpParseOptions(languageVersion)));

            List<MetadataReference> references = [MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location)];

            if (additionalReferences is not null)
            {
                foreach (var reference in additionalReferences)
                {
                    references.Add(MetadataReference.CreateFromFile(reference.Location));
                }
            }

            return CSharpCompilation.Create(
                "TestAssembly",
                syntaxTrees,
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }
    }

    internal record GeneratedSourceResult(string HintName, SyntaxTree SyntaxTree, string Source);
}
