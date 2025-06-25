using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SepCsvSourceGenerator;

[Generator]
public partial class CsvGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all methods with [GenerateCsvParser]
        IncrementalValuesProvider<MethodDeclarationSyntax> methodDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "SepCsvSourceGenerator.GenerateCsvParserAttribute",
                (node, _) => node is MethodDeclarationSyntax,
                (context, _) => (MethodDeclarationSyntax)context.TargetNode)
            .Where(static m => m is not null);

        IncrementalValueProvider<(Compilation, ImmutableArray<MethodDeclarationSyntax>)> compilationAndMethods =
            context.CompilationProvider.Combine(methodDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndMethods, static (spc, source) => Execute(source.Item1, source.Item2, spc));
    }

    private static void Execute(Compilation compilation, ImmutableArray<MethodDeclarationSyntax> methods, SourceProductionContext context)
    {
        if (methods.IsDefaultOrEmpty)
        {
            // nothing to do yet
            return;
        }

        var p = new Parser(compilation, context.ReportDiagnostic, context.CancellationToken);
        var parseTargets = p.GetParseTargets(methods);
        if (parseTargets.Count > 0)
        {
            var e = new Emitter();
            string result = e.Emit(parseTargets, context.CancellationToken);
            context.AddSource("CsvParser.g.cs", result);
        }
    }
}
