using System;
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
        private readonly CancellationToken _cancellationToken;
        private readonly Compilation _compilation;
        private readonly Action<Diagnostic> _reportDiagnostic;

        public Parser(Compilation compilation, Action<Diagnostic> reportDiagnostic, CancellationToken cancellationToken)
        {
            _compilation = compilation;
            _reportDiagnostic = reportDiagnostic;
            _cancellationToken = cancellationToken;
        }

        public IReadOnlyList<CsvMethod> GetCsvMethods(IEnumerable<MethodDeclarationSyntax> methods)
        {
            var results = new List<CsvMethod>();

            // Group by syntax tree to minimize semantic model creation
            foreach (IGrouping<SyntaxTree, MethodDeclarationSyntax> group in methods.GroupBy(x => x.SyntaxTree))
            {
                SemanticModel semanticModel = _compilation.GetSemanticModel(group.Key);

                foreach (MethodDeclarationSyntax method in group)
                {
                    _cancellationToken.ThrowIfCancellationRequested();

                    var csvMethod = AnalyzeMethod(method, semanticModel);
                    if (csvMethod != null)
                    {
                        results.Add(csvMethod);
                    }
                }
            }

            return results;
        }

        private CsvMethod? AnalyzeMethod(MethodDeclarationSyntax methodSyntax, SemanticModel semanticModel)
        {
            IMethodSymbol methodSymbol = semanticModel.GetDeclaredSymbol(methodSyntax, _cancellationToken)!;
            if (methodSymbol == null)
            {
                return null;
            }

            // Validate method
            if (!ValidateMethod(methodSymbol, methodSyntax))
            {
                return null;
            }

            // Get containing type info
            var containingType = methodSymbol.ContainingType;
            var properties = GetCsvProperties(containingType);

            return new CsvMethod
            {
                Namespace = containingType.ContainingNamespace.ToDisplayString(),
                ContainingTypeName = containingType.Name,
                MethodName = methodSymbol.Name,
                Properties = properties,
                IsPartial = methodSyntax.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword))
            };
        }

        private bool ValidateMethod(IMethodSymbol methodSymbol, MethodDeclarationSyntax methodSyntax)
        {
            // Must be static
            if (!methodSymbol.IsStatic)
            {
                _reportDiagnostic(
                    Diagnostic.Create(DiagnosticDescriptors.MethodMustBeStatic, methodSyntax.GetLocation()));
                return false;
            }

            // Must be partial
            if (!methodSyntax.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
            {
                _reportDiagnostic(
                    Diagnostic.Create(DiagnosticDescriptors.MethodMustBePartial, methodSyntax.GetLocation()));
                return false;
            }

            // Must return IAsyncEnumerable<T>
            var returnType = methodSymbol.ReturnType as INamedTypeSymbol;
            if (returnType?.ConstructedFrom.ToDisplayString() != "System.Collections.Generic.IAsyncEnumerable<T>")
            {
                _reportDiagnostic(
                    Diagnostic.Create(DiagnosticDescriptors.InvalidReturnType, methodSyntax.ReturnType.GetLocation()));
                return false;
            }

            // Must have SepReader parameter
            if (methodSymbol.Parameters.Length < 1 ||
                methodSymbol.Parameters[0].Type.ToDisplayString() != "nietras.SeparatedValues.SepReader")
            {
                _reportDiagnostic(
                    Diagnostic.Create(DiagnosticDescriptors.MissingSepReaderParameter, methodSyntax.GetLocation()));
                return false;
            }

            return true;
        }

        private ImmutableArray<CsvProperty> GetCsvProperties(INamedTypeSymbol typeSymbol)
        {
            var properties = new List<CsvProperty>();

            foreach (var member in typeSymbol.GetMembers())
            {
                if (member is not IPropertySymbol propertySymbol)
                    continue;

                var headerNameAttr = propertySymbol.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.Name == "CsvHeaderNameAttribute");

                if (headerNameAttr == null)
                    continue;

                var headerName = headerNameAttr.ConstructorArguments[0].Value as string;
                if (string.IsNullOrEmpty(headerName))
                    continue;

                var dateFormatAttr = propertySymbol.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.Name == "CsvDateFormatAttribute");

                var dateFormat = dateFormatAttr?.ConstructorArguments[0].Value as string;

                properties.Add(new CsvProperty
                {
                    PropertyName = propertySymbol.Name,
                    HeaderName = headerName,
                    TypeName = propertySymbol.Type.ToDisplayString(),
                    DateFormat = dateFormat,
                    IsRequired = propertySymbol.IsRequired,
                    IsNullable = propertySymbol.NullableAnnotation == NullableAnnotation.Annotated
                });
            }

            return properties.ToImmutableArray();
        }
    }

    internal sealed class CsvMethod
    {
        public string Namespace { get; set; } = "";
        public string ContainingTypeName { get; set; } = "";
        public string MethodName { get; set; } = "";
        public bool IsPartial { get; set; }
        public ImmutableArray<CsvProperty> Properties { get; set; }
    }

    internal sealed class CsvProperty
    {
        public string PropertyName { get; set; } = "";
        public string HeaderName { get; set; } = "";
        public string TypeName { get; set; } = "";
        public string? DateFormat { get; set; }
        public bool IsRequired { get; set; }
        public bool IsNullable { get; set; }
    }

    internal static class DiagnosticDescriptors
    {
        public static readonly DiagnosticDescriptor MethodMustBeStatic = new(
            id: "CSV001",
            title: "CSV parser method must be static",
            messageFormat: "The CSV parser method must be static",
            category: "CsvSourceGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MethodMustBePartial = new(
            id: "CSV002",
            title: "CSV parser method must be partial",
            messageFormat: "The CSV parser method must be partial",
            category: "CsvSourceGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidReturnType = new(
            id: "CSV003",
            title: "Invalid return type",
            messageFormat: "The CSV parser method must return IAsyncEnumerable<T>",
            category: "CsvSourceGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MissingSepReaderParameter = new(
            id: "CSV004",
            title: "Missing SepReader parameter",
            messageFormat: "The CSV parser method must have a SepReader parameter",
            category: "CsvSourceGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}