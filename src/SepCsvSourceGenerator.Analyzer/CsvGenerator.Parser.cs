using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

namespace SepCsvSourceGenerator;

public partial class CsvGenerator
{
    internal sealed class Parser
    {
        internal const string GenerateCsvParserAttribute = "SepCsvSourceGenerator.GenerateCsvParserAttribute";

        private readonly Compilation _compilation;
        private readonly Action<Diagnostic> _reportDiagnostic;
        private readonly CancellationToken _cancellationToken;

        public Parser(Compilation compilation, Action<Diagnostic> reportDiagnostic, CancellationToken cancellationToken)
        {
            _compilation = compilation;
            _reportDiagnostic = reportDiagnostic;
            _cancellationToken = cancellationToken;
        }

        public IReadOnlyList<CsvParseTarget> GetCsvParseTargets(ImmutableArray<MethodDeclarationSyntax> methods)
        {
            var result = new List<CsvParseTarget>();
            foreach (var method in methods)
            {
                if (method == null) continue;
                var sm = _compilation.GetSemanticModel(method.SyntaxTree);
                var methodSymbol = sm.GetDeclaredSymbol(method, _cancellationToken) as IMethodSymbol;
                if (methodSymbol == null) continue;

                // Check for [GenerateCsvParser]
                bool hasAttr = methodSymbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == GenerateCsvParserAttribute);
                if (!hasAttr) continue;

                // Must be static partial
                if (!methodSymbol.IsStatic || !method.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                    continue;

                // Get containing class
                var classSymbol = methodSymbol.ContainingType;
                if (classSymbol == null) continue;

                // Get properties with [CsvHeaderName]
                var properties = classSymbol.GetMembers().OfType<IPropertySymbol>()
                    .Where(p => p.GetAttributes().Any(a => a.AttributeClass?.Name == "CsvHeaderNameAttribute"))
                    .Select(p => new CsvPropertyInfo
                    {
                        Name = p.Name,
                        Type = p.Type.ToDisplayString(),
                        HeaderName = p.GetAttributes().First(a => a.AttributeClass?.Name == "CsvHeaderNameAttribute").ConstructorArguments[0].Value?.ToString() ?? p.Name,
                        DateFormat = p.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "CsvDateFormatAttribute")?.ConstructorArguments.FirstOrDefault().Value?.ToString(),
                        IsRequired = p.NullableAnnotation != NullableAnnotation.Annotated && !p.Type.NullableAnnotation.HasFlag(NullableAnnotation.Annotated)
                    }).ToList();

                result.Add(new CsvParseTarget
                {
                    ClassName = classSymbol.Name,
                    Namespace = classSymbol.ContainingNamespace.ToDisplayString(),
                    MethodName = methodSymbol.Name,
                    Properties = properties
                });
            }
            return result;
        }

        internal class CsvParseTarget
        {
            public string ClassName { get; set; } = string.Empty;
            public string Namespace { get; set; } = string.Empty;
            public string MethodName { get; set; } = string.Empty;
            public List<CsvPropertyInfo> Properties { get; set; } = new();
        }

        internal class CsvPropertyInfo
        {
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string HeaderName { get; set; } = string.Empty;
            public string? DateFormat { get; set; }
            public bool IsRequired { get; set; }
        }
    }
}
