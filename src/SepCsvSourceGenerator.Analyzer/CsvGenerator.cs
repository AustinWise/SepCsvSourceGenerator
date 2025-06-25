using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
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
        // Find methods with [GenerateCsvParser]
        var parserMethods = context.SyntaxProvider.ForAttributeWithMetadataName(
            "SepCsvSourceGenerator.GenerateCsvParserAttribute",
            predicate: static (node, _) => node is MethodDeclarationSyntax,
            transform: static (ctx, _) => ctx
        );

        context.RegisterSourceOutput(parserMethods, static (spc, ctx) =>
        {
            var methodSyntax = (MethodDeclarationSyntax)ctx.TargetNode;
            var classSyntax = methodSyntax.Parent as ClassDeclarationSyntax;
            if (classSyntax == null) return;
            var semanticModel = ctx.SemanticModel;
            var classSymbol = semanticModel.GetDeclaredSymbol(classSyntax) as INamedTypeSymbol;
            if (classSymbol == null) return;

            var properties = classSymbol.GetMembers().OfType<IPropertySymbol>()
                .Where(p => p.GetAttributes().Any(a => a.AttributeClass?.Name == "CsvHeaderNameAttribute"))
                .ToList();

            var requiredProps = properties.Where(p => p.GetAttributes().Any(a => a.AttributeClass?.Name == "RequiredMemberAttribute" || p.SetMethod == null)).ToList();
            var optionalProps = properties.Where(p => !requiredProps.Contains(p)).ToList();

            var headerLookups = new StringBuilder();
            var assignments = new StringBuilder();
            var optionals = new StringBuilder();
            var ndxVars = new StringBuilder();
            var propParsers = new StringBuilder();

            foreach (var prop in properties)
            {
                var headerAttr = prop.GetAttributes().First(a => a.AttributeClass?.Name == "CsvHeaderNameAttribute");
                var headerName = headerAttr.ConstructorArguments[0].Value?.ToString() ?? prop.Name;
                var ndxVar = prop.Name.ToLowerInvariant() + "Ndx";
                ndxVars.AppendLine($"        int {ndxVar};");
                if (requiredProps.Contains(prop))
                {
                    headerLookups.AppendLine($"        if (!reader.Header.TryIndexOf(\"{headerName}\", out {ndxVar}))");
                    headerLookups.AppendLine($"            throw new ArgumentException(\"Missing column name '{headerName}'\");");
                }
                else
                {
                    headerLookups.AppendLine($"        if (!reader.Header.TryIndexOf(\"{headerName}\", out {ndxVar}))");
                    headerLookups.AppendLine($"            {ndxVar} = -1;");
                }
            }

            foreach (var prop in properties)
            {
                var ndxVar = prop.Name.ToLowerInvariant() + "Ndx";
                var type = prop.Type.ToDisplayString();
                var dateFormatAttr = prop.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "CsvDateFormatAttribute");
                if (dateFormatAttr != null)
                {
                    var format = dateFormatAttr.ConstructorArguments[0].Value?.ToString() ?? "";
                    assignments.AppendLine($"                {prop.Name} = DateTime.ParseExact(row[{ndxVar}].Span, \"{format}\", CultureInfo.InvariantCulture),");
                }
                else if (type == "string" || type == "string?")
                {
                    assignments.AppendLine($"                {prop.Name} = row[{ndxVar}].Span.ToString(),");
                }
                else if (type.EndsWith("?"))
                {
                    // nullable
                    optionals.AppendLine($"                if ({ndxVar} != -1)");
                    optionals.AppendLine($"                    ret.{prop.Name} = {type.TrimEnd('?')} .Parse(row[{ndxVar}].Span, CultureInfo.InvariantCulture);");
                }
                else
                {
                    assignments.AppendLine($"                {prop.Name} = {type}.Parse(row[{ndxVar}].Span, CultureInfo.InvariantCulture),");
                }
            }

            var className = classSymbol.Name;
            var ns = classSymbol.ContainingNamespace.IsGlobalNamespace ? "" : $"namespace {classSymbol.ContainingNamespace};\n";
            var methodName = methodSyntax.Identifier.Text;
            var generated = $@"
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;

{ns}

partial class {className}
{{
    public async static partial IAsyncEnumerable<{className}> {methodName}(nietras.SeparatedValues.SepReader reader, [EnumeratorCancellation] System.Threading.CancellationToken ct)
    {{
{ndxVars}
{headerLookups}
        await foreach (var row in reader)
        {{
            ct.ThrowIfCancellationRequested();
            var ret = new {className}
            {{
{assignments}            }};
{optionals}            yield return ret;
        }}
    }}
}}
";
            spc.AddSource($"{className}_{methodName}_CsvParser.g.cs", SourceText.From(generated, Encoding.UTF8));
        });
    }
}
