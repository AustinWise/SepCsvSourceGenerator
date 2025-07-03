// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SepCsvSourceGenerator.Analyzer.Tests
{
    internal static class RoslynTestUtils
    {
        public static (ImmutableArray<Diagnostic>, ImmutableArray<GeneratedSourceResult>) RunGenerator(
            IIncrementalGenerator generator,
            Assembly[]? additionalReferences,
            string[] sources,
            bool includeBaseReferences = true,
            LanguageVersion languageVersion = LanguageVersion.Default,
            CancellationToken cancellationToken = default)
        {
            var compilation = CreateCompilation(sources, additionalReferences, includeBaseReferences, languageVersion);
            var (outputCompilation, generatorDiagnostics) = RunGenerator(compilation, generator, cancellationToken);
            var generatedSources = outputCompilation.SyntaxTrees
                .Where(t => !compilation.SyntaxTrees.Any(t2 => t2.FilePath == t.FilePath))
                .Select(t => new GeneratedSourceResult(t, t.GetText(cancellationToken).ToString()))
                .ToImmutableArray();

            return (generatorDiagnostics, generatedSources);
        }

        public static (Compilation, ImmutableArray<Diagnostic>) RunGenerator(Compilation compilation, IIncrementalGenerator generator, CancellationToken cancellationToken)
        {
            var driver = CSharpGeneratorDriver.Create([generator.AsSourceGenerator()], [], (CSharpParseOptions)compilation.SyntaxTrees.First().Options);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics, cancellationToken);
            return (outputCompilation, diagnostics);
        }

        public static Compilation CreateCompilation(
            string[] sources,
            Assembly[]? additionalReferences = null,
            bool includeBaseReferences = true,
            LanguageVersion languageVersion = LanguageVersion.Default)
        {
            var syntaxTrees = sources.Select(s => CSharpSyntaxTree.ParseText(s, new CSharpParseOptions(languageVersion)));

            List<MetadataReference> references = [];
            if (includeBaseReferences)
            {
                references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
                references.Add(MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location));
            }

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

    internal record GeneratedSourceResult(SyntaxTree SyntaxTree, string Source);
}
