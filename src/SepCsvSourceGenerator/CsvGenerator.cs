using Microsoft.CodeAnalysis;

namespace SepCsvSourceGenerator;

[Generator]
public partial class CsvGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Get all methods with [GenerateCsvParser] attribute
        IncrementalValuesProvider<MethodDeclarationSyntax> methodDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "SepCsvSourceGenerator.GenerateCsvParserAttribute",
                (node, _) => node is MethodDeclarationSyntax,
                (context, _) => (MethodDeclarationSyntax)context.TargetNode);

        // Combine the compilation and methods
        IncrementalValueProvider<(Compilation, ImmutableArray<MethodDeclarationSyntax>)> compilationAndMethods =
            context.CompilationProvider.Combine(methodDeclarations.Collect());

        // Register the source output
        context.RegisterSourceOutput(compilationAndMethods, static (spc, source) => Execute(source.Item1, source.Item2, spc));
    }

    private static void Execute(Compilation compilation, ImmutableArray<MethodDeclarationSyntax> methods, SourceProductionContext context)
    {
        if (methods.IsDefaultOrEmpty)
        {
            // Nothing to do
            return;
        }

        var p = new Parser(compilation, context.ReportDiagnostic, context.CancellationToken);
        var csvMethods = p.GetCsvMethods(methods);
        if (csvMethods.Count > 0)
        {
            var e = new Emitter();
            string result = e.Emit(csvMethods, context.CancellationToken);
            context.AddSource("CsvParser.g.cs", result);
        }
    }
} 