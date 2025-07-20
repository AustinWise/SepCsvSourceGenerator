using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace AWise.SepCsvSourceGenerator.Analyzer;

[Generator]
public partial class CsvGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static spc =>
        {
            spc.AddEmbeddedAttributeDefinition();
            spc.AddSource("SepCsvSourceGenerator.Attributes.cs", SourceText.From(
                """
                namespace AWise.SepCsvSourceGenerator
                {
                    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
                    internal abstract class CsvAttribute : global::System.Attribute { }

                    /// <summary>
                    /// For date and time types, this specifies the format string used with <c>ParseExact</c>.
                    /// </summary>
                    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
                    [global::System.AttributeUsage(global::System.AttributeTargets.Property | global::System.AttributeTargets.Field, AllowMultiple = false)]
                    internal sealed class CsvDateFormatAttribute : CsvAttribute
                    {
                        public CsvDateFormatAttribute([global::System.Diagnostics.CodeAnalysis.StringSyntax("DateTimeFormat")] string format) { Format = format; }
                        public string Format { get; }
                    }

                    /// <summary>
                    /// The name of the column in the CSV file header this property is mapped to.
                    /// </summary>
                    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
                    [global::System.AttributeUsage(global::System.AttributeTargets.Property | global::System.AttributeTargets.Field, AllowMultiple = false)]
                    internal sealed class CsvHeaderNameAttribute : CsvAttribute
                    {
                        public CsvHeaderNameAttribute(params string[] names) { }
                    }

                    /// <summary>
                    /// When placed on a partial method declaration, generates a CSV parser.
                    /// </summary>
                    /// <remarks>
                    /// The return type of the method must be either <c>IEnumerable&lt;T&gt;</c> or <c>IAsyncEnumerable&lt;T&gt;</c>.
                    /// The first parameter must be a <c>SepReader</c>. Optionally, the second parameter can be a <c>CancellationToken</c>.
                    /// </remarks>
                    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
                    [global::System.AttributeUsage(global::System.AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
                    internal sealed class GenerateCsvParserAttribute : CsvAttribute
                    {
                        public GenerateCsvParserAttribute()
                        {
                        }

                        /// <summary>
                        /// If true, automatically map all public properties.
                        /// If false, only properties marked with <see cref="CsvHeaderNameAttribute" /> are mapped.
                        /// </summary>
                        public bool IncludeProperties { get; set; }
                    }
                }
                """, Encoding.UTF8));
        });
        IncrementalValuesProvider<MethodDeclarationSyntax> methodDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                Parser.GenerateCsvParserAttributeFullName,
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
            var emitter = new Emitter();
            var methodsByClass = csvMethodsToGenerate.GroupBy(m => m.ContainingClassSymbol, SymbolEqualityComparer.Default);

            foreach (var group in methodsByClass)
            {
                INamedTypeSymbol? key = (INamedTypeSymbol?)group.Key ?? throw new Exception("Grouping symbol is null for some reason?");
                string? sourceText = emitter.Emit(key, [.. group], context.CancellationToken);
                if (!string.IsNullOrEmpty(sourceText))
                {
                    context.AddSource(GetHintName(key), SourceText.From(sourceText!, Encoding.UTF8));
                }
            }
        }
    }

    private static string SanitizeFileName(string name) => name.Replace("<", "_").Replace(">", "_").Replace("?", "_").Replace("*", "_");

    private static string GetHintName(INamedTypeSymbol classSymbol)
    {
        // Create a unique file name, e.g., Namespace.ClassName.CsvGenerator.g.cs
        // Replace invalid filename characters from namespace and class name
        var parts = new List<string>();
        if (!classSymbol.ContainingNamespace.IsGlobalNamespace)
        {
            parts.Add(SanitizeFileName(classSymbol.ContainingNamespace.ToDisplayString()));
        }

        var typeHierarchy = new Stack<string>();
        INamedTypeSymbol? current = classSymbol;
        while (current != null)
        {
            typeHierarchy.Push(SanitizeFileName(current.Name));
            current = current.ContainingType;
        }
        parts.AddRange(typeHierarchy);
        return string.Join(".", parts) + ".CsvGenerator.g.cs";
    }
}
