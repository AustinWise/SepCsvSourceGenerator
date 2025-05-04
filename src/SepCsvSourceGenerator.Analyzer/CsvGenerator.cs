using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using SepCsvSourceGenerator.Analyzer;
using System.Collections.Immutable;
using System.Text;

// TODO: consider multi targeting this package to also target rosyln 3.11, for VS 2019 support.
// See how the nuget Microsoft.Extensions.Logging.Abstractions is implemented

namespace SepCsvSourceGenerator;

[Generator]
public class CsvGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                Parser.GenerateCsvParserAttribute,
                (node, _) => node is MethodDeclarationSyntax,
                (context, _) => context.TargetNode.Parent as ClassDeclarationSyntax)
            .Where(static m => m is not null)!;

        IncrementalValueProvider<(Compilation, ImmutableArray<ClassDeclarationSyntax>)> compilationAndClasses =
            context.CompilationProvider.Combine(classDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndClasses, static (spc, source) => Execute(source.Item1, source.Item2, spc));
    }

    private static void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes, SourceProductionContext context)
    {
        if (classes.IsDefaultOrEmpty)
        {
            return;
        }

        ImmutableHashSet<ClassDeclarationSyntax> distinctClasses = classes.ToImmutableHashSet();

        var p = new Parser(compilation, context.ReportDiagnostic, context.CancellationToken);
        IReadOnlyList<CsvClass> csvClasses = p.GetCsvClasses(distinctClasses);
        if (csvClasses.Count > 0)
        {
            var e = new Emitter();
            string result = e.Emit(csvClasses, context.CancellationToken);

            context.AddSource("SepCsvParsing.g.cs", SourceText.From(result, Encoding.UTF8));
        }
    }
}
