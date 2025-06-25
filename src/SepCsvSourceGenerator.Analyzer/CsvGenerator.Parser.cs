using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SepCsvSourceGenerator;

public partial class CsvGenerator
{
    internal sealed class Parser
    {
        private readonly Compilation _compilation;
        private readonly Action<Diagnostic> _reportDiagnostic;
        private readonly CancellationToken _cancellationToken;

        public Parser(Compilation compilation, Action<Diagnostic> reportDiagnostic, CancellationToken cancellationToken)
        {
            _compilation = compilation;
            _reportDiagnostic = reportDiagnostic;
            _cancellationToken = cancellationToken;
        }

        public IReadOnlyList<ParseTarget> GetParseTargets(IEnumerable<MethodDeclarationSyntax> methods)
        {
            var result = new List<ParseTarget>();
            foreach (var method in methods)
            {
                var model = _compilation.GetSemanticModel(method.SyntaxTree);
                var methodSymbol = model.GetDeclaredSymbol(method, _cancellationToken) as IMethodSymbol;
                if (methodSymbol == null)
                    continue;
                // Only static partial methods
                if (!methodSymbol.IsStatic || !methodSymbol.IsPartialDefinition)
                    continue;
                // Must return IAsyncEnumerable<T>
                var returnType = methodSymbol.ReturnType;
                if (returnType is not INamedTypeSymbol namedReturnType ||
                    namedReturnType.Name != "IAsyncEnumerable" ||
                    namedReturnType.TypeArguments.Length != 1)
                    continue;
                var targetType = namedReturnType.TypeArguments[0] as INamedTypeSymbol;
                if (targetType == null)
                    continue;
                // Collect properties
                var properties = new List<ParseProperty>();
                foreach (var member in targetType.GetMembers().OfType<IPropertySymbol>())
                {
                    var headerName = member.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "CsvHeaderNameAttribute")?.ConstructorArguments.FirstOrDefault().Value as string;
                    var dateFormat = member.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "CsvDateFormatAttribute")?.ConstructorArguments.FirstOrDefault().Value as string;
                    bool required = member.SetMethod == null || member.SetMethod.DeclaredAccessibility == Accessibility.Private || member.IsRequired;
                    properties.Add(new ParseProperty
                    {
                        Name = member.Name,
                        Type = member.Type.ToDisplayString(),
                        HeaderName = headerName,
                        DateFormat = dateFormat,
                        Required = required,
                        IsNullable = member.NullableAnnotation == NullableAnnotation.Annotated || member.Type.NullableAnnotation == NullableAnnotation.Annotated
                    });
                }
                result.Add(new ParseTarget
                {
                    MethodSymbol = methodSymbol,
                    TargetType = targetType,
                    Properties = properties
                });
            }
            return result;
        }
    }

    internal sealed class ParseTarget
    {
        public IMethodSymbol MethodSymbol { get; set; } = null!;
        public INamedTypeSymbol TargetType { get; set; } = null!;
        public List<ParseProperty> Properties { get; set; } = null!;
    }

    internal sealed class ParseProperty
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? HeaderName { get; set; }
        public string? DateFormat { get; set; }
        public bool Required { get; set; }
        public bool IsNullable { get; set; }
    }
}
