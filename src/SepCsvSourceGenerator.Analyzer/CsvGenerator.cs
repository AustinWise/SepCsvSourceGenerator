using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace SepCsvSourceGenerator;

[Generator]
public partial class CsvGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all methods with [GenerateCsvParser] attribute
        var methodDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                SepCsvSourceGenerator.CsvGenerator.Parser.GenerateCsvParserAttribute,
                (node, _) => node is MethodDeclarationSyntax,
                (ctx, _) => ctx.TargetNode as MethodDeclarationSyntax)
            .Where(static m => m is not null)
            .Select(static (m, _) => m!); // ensure non-null

        var compilationAndMethods = context.CompilationProvider.Combine(methodDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndMethods, static (spc, source) => Execute(source.Item1, source.Item2, spc));
    }

    private static void Execute(Compilation compilation, ImmutableArray<MethodDeclarationSyntax> methods, SourceProductionContext context)
    {
        if (methods.IsDefaultOrEmpty)
        {
            // nothing to do yet
            return;
        }

        var parser = new SepCsvSourceGenerator.CsvGenerator.Parser(compilation, context.ReportDiagnostic, context.CancellationToken);
        var parseTargets = parser.GetCsvParseTargets(methods);
        if (parseTargets.Count > 0)
        {
            var emitter = new SepCsvSourceGenerator.CsvGenerator.Emitter();
            string result = emitter.Emit(parseTargets, context.CancellationToken);
            context.AddSource("CsvParser.g.cs", SourceText.From(result, Encoding.UTF8));
        }
    }
}
