using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SepCsvSourceGenerator;

[Generator]
public partial class CsvGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var methodDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (s, _) => s is MethodDeclarationSyntax,
                transform: (ctx, _) => GetCandidateMethodSymbol(ctx))
            .Where(m => m is not null)
            .Collect();

        context.RegisterSourceOutput(methodDeclarations, (spc, methods) =>
        {
            foreach (var tuple in methods.OfType<(IMethodSymbol, INamedTypeSymbol)>())
            {
                var methodSymbol = tuple.Item1;
                var classSymbol = tuple.Item2;
                var source = GenerateParser(classSymbol, methodSymbol);
                if (!string.IsNullOrEmpty(source))
                {
                    spc.AddSource($"{classSymbol.Name}_CsvParser.g.cs", source);
                }
            }
        });
    }

    private static (IMethodSymbol, INamedTypeSymbol)? GetCandidateMethodSymbol(GeneratorSyntaxContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        foreach (var attrList in method.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var name = attr.Name.ToString();
                if (name.Contains("GenerateCsvParser"))
                {
                    var methodSymbol = context.SemanticModel.GetDeclaredSymbol(method) as IMethodSymbol;
                    if (methodSymbol != null && methodSymbol.GetAttributes().Any(a => a.AttributeClass?.Name == "GenerateCsvParserAttribute"))
                    {
                        var classSymbol = methodSymbol.ContainingType;
                        return (methodSymbol, classSymbol);
                    }
                }
            }
        }
        return null;
    }

    private static string GenerateParser(INamedTypeSymbol classSymbol, IMethodSymbol methodSymbol)
    {
        var sb = new StringBuilder();
        var ns = classSymbol.ContainingNamespace.ToDisplayString();
        var className = classSymbol.Name;
        var methodName = methodSymbol.Name;
        var properties = classSymbol.GetMembers().OfType<IPropertySymbol>().ToList();

        sb.AppendLine($"namespace {ns}");
        sb.AppendLine("{");
        sb.AppendLine($"    public partial class {className}");
        sb.AppendLine("    {");
        sb.AppendLine($"        public async static partial System.Collections.Generic.IAsyncEnumerable<{className}> {methodName}(nietras.SeparatedValues.SepReader reader, [System.Runtime.CompilerServices.EnumeratorCancellation] System.Threading.CancellationToken ct)");
        sb.AppendLine("        {");
        // Index declarations
        foreach (var prop in properties)
        {
            var headerAttr = prop.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "CsvHeaderNameAttribute");
            if (headerAttr != null)
            {
                var headerName = headerAttr.ConstructorArguments[0].Value?.ToString();
                sb.AppendLine($"            int {prop.Name.ToLower()}Ndx;");
            }
        }
        sb.AppendLine();
        // Required member checks
        foreach (var prop in properties)
        {
            var headerAttr = prop.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "CsvHeaderNameAttribute");
            if (headerAttr != null)
            {
                var headerName = headerAttr.ConstructorArguments[0].Value?.ToString();
                var isRequired = prop.NullableAnnotation == NullableAnnotation.NotAnnotated && !prop.Type.NullableAnnotation.HasFlag(NullableAnnotation.Annotated);
                if (isRequired)
                {
                    sb.AppendLine($"            if (!reader.Header.TryIndexOf(\"{headerName}\", out {prop.Name.ToLower()}Ndx))");
                    sb.AppendLine($"            {{");
                    sb.AppendLine($"                throw new System.ArgumentException(\"Missing column name '{headerName}'\");");
                    sb.AppendLine($"            }}");
                }
            }
        }
        // Not required member checks
        foreach (var prop in properties)
        {
            var headerAttr = prop.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "CsvHeaderNameAttribute");
            if (headerAttr != null)
            {
                var headerName = headerAttr.ConstructorArguments[0].Value?.ToString();
                var isRequired = prop.NullableAnnotation == NullableAnnotation.NotAnnotated && !prop.Type.NullableAnnotation.HasFlag(NullableAnnotation.Annotated);
                if (!isRequired)
                {
                    sb.AppendLine($"            if (!reader.Header.TryIndexOf(\"{headerName}\", out {prop.Name.ToLower()}Ndx))");
                    sb.AppendLine($"            {{");
                    sb.AppendLine($"                {prop.Name.ToLower()}Ndx = -1;");
                    sb.AppendLine($"            }}");
                }
            }
        }
        sb.AppendLine();
        sb.AppendLine("            await foreach (nietras.SeparatedValues.SepReader.Row row in reader)");
        sb.AppendLine("            {");
        sb.AppendLine("                ct.ThrowIfCancellationRequested();");
        sb.AppendLine($"                var ret = new {className}()");
        sb.AppendLine("                {");
        foreach (var prop in properties)
        {
            var headerAttr = prop.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "CsvHeaderNameAttribute");
            if (headerAttr != null)
            {
                var headerName = headerAttr.ConstructorArguments[0].Value?.ToString();
                var dateFormatAttr = prop.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "CsvDateFormatAttribute");
                var type = prop.Type.ToDisplayString();
                var ndx = prop.Name.ToLower() + "Ndx";
                if (dateFormatAttr != null)
                {
                    var fmt = dateFormatAttr.ConstructorArguments[0].Value?.ToString();
                    sb.AppendLine($"                    {prop.Name} = System.DateTime.ParseExact(row[{ndx}].Span, \"{fmt}\", System.Globalization.CultureInfo.InvariantCulture),");
                }
                else if (type == "string")
                {
                    sb.AppendLine($"                    {prop.Name} = row[{ndx}].Span.ToString(),");
                }
                else if (type.EndsWith("?"))
                {
                    // nullable type, handled below
                }
                else
                {
                    sb.AppendLine($"                    {prop.Name} = {type}.Parse(row[{ndx}].Span, System.Globalization.CultureInfo.InvariantCulture),");
                }
            }
        }
        sb.AppendLine("                };");
        // Nullable properties
        foreach (var prop in properties)
        {
            var headerAttr = prop.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "CsvHeaderNameAttribute");
            if (headerAttr != null && prop.Type.NullableAnnotation == NullableAnnotation.Annotated)
            {
                var type = prop.Type.ToDisplayString().TrimEnd('?');
                var ndx = prop.Name.ToLower() + "Ndx";
                sb.AppendLine($"                if ({ndx} != -1)");
                sb.AppendLine($"                {{");
                sb.AppendLine($"                    ret.{prop.Name} = {type}.Parse(row[{ndx}].Span, System.Globalization.CultureInfo.InvariantCulture);");
                sb.AppendLine($"                }}");
            }
        }
        sb.AppendLine("                yield return ret;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
