using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace US.AWise.SepCsvSourceGenerator.Analyzer;

[Generator]
public partial class CsvGenerator : IIncrementalGenerator
{
    public const string GenerateCsvParserAttributeFullName = "US.AWise.SepCsvSourceGenerator.GenerateCsvParserAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static spc =>
        {
            spc.AddEmbeddedAttributeDefinition();
            spc.AddSource("SepCsvSourceGenerator.Attributes.cs", SourceText.From(
                """
                namespace US.AWise.SepCsvSourceGenerator
                {
                    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
                    internal abstract class CsvAttribute : global::System.Attribute { }

                    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
                    [global::System.AttributeUsage(global::System.AttributeTargets.Property | global::System.AttributeTargets.Field, AllowMultiple = false)]
                    internal sealed class CsvDateFormatAttribute : CsvAttribute
                    {
                        public CsvDateFormatAttribute(string format) { Format = format; }
                        public string Format { get; }
                    }

                    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
                    [global::System.AttributeUsage(global::System.AttributeTargets.Property | global::System.AttributeTargets.Field, AllowMultiple = false)]
                    internal sealed class CsvHeaderNameAttribute : CsvAttribute
                    {
                        public CsvHeaderNameAttribute(string name) { Name = name; }
                        public string Name { get; }
                    }

                    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
                    [global::System.AttributeUsage(global::System.AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
                    internal sealed class GenerateCsvParserAttribute : CsvAttribute
                    {
                        public GenerateCsvParserAttribute()
                        {
                        }
                    }
                }
                """, Encoding.UTF8));
        });
        IncrementalValuesProvider<MethodDeclarationSyntax> methodDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                GenerateCsvParserAttributeFullName,
                predicate: static (node, _) => node is MethodDeclarationSyntax,
                transform: static (context, _) => (MethodDeclarationSyntax)context.TargetNode);

        IncrementalValueProvider<(Compilation, ImmutableArray<MethodDeclarationSyntax>)> compilationAndMethods =
            context.CompilationProvider.Combine(methodDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndMethods, static (spc, source) =>
            Execute(source.Item1, source.Item2, spc));
    }

    private static void Execute(Compilation compilation, ImmutableArray<MethodDeclarationSyntax> methods, SourceProductionContext context)
    {
        if (methods.IsDefaultOrEmpty)
        {
            return;
        }

        var parser = new Parser(compilation, context.ReportDiagnostic, context.CancellationToken);
        var csvMethodsToGenerate = parser.GetCsvMethodDefinitions(methods);

        if (csvMethodsToGenerate.Count > 0)
        {
            var emitter = new Emitter(context.ReportDiagnostic);
            var methodsByClass = csvMethodsToGenerate.GroupBy(m => m.ContainingClassSymbol, SymbolEqualityComparer.Default);

            foreach (var group in methodsByClass)
            {
                INamedTypeSymbol? key = (INamedTypeSymbol?)group.Key ?? throw new Exception("Grouping symbol is null for some reason?");
                string? sourceText = emitter.Emit(key, [.. group], context.CancellationToken);
                if (!string.IsNullOrEmpty(sourceText))
                {
                    context.AddSource(Emitter.GetHintName(key), SourceText.From(sourceText!, Encoding.UTF8));
                }
            }
        }
    }
}
