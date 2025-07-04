using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace SepCsvSourceGenerator;

public partial class CsvGenerator
{
    internal sealed class Parser(Compilation compilation, Action<Diagnostic> reportDiagnostic, CancellationToken cancellationToken)
    {
        private readonly Compilation _compilation = compilation;
        private readonly Action<Diagnostic> _reportDiagnostic = reportDiagnostic;
        private readonly CancellationToken _cancellationToken = cancellationToken;

        private readonly INamedTypeSymbol? _generateCsvParserAttributeSymbol = compilation.GetTypeByMetadataName(GenerateCsvParserAttributeFullName);
        private readonly INamedTypeSymbol? _csvHeaderNameAttributeSymbol = compilation.GetTypeByMetadataName("SepCsvSourceGenerator.CsvHeaderNameAttribute");
        private readonly INamedTypeSymbol? _csvDateFormatAttributeSymbol = compilation.GetTypeByMetadataName("SepCsvSourceGenerator.CsvDateFormatAttribute");
        private readonly INamedTypeSymbol? _sepReaderSymbol = compilation.GetTypeByMetadataName("nietras.SeparatedValues.SepReader");
        private readonly INamedTypeSymbol? _iAsyncEnumerableSymbol = compilation.GetTypeByMetadataName("System.Collections.Generic.IAsyncEnumerable`1");
        private readonly INamedTypeSymbol? _cancellationTokenSymbol = compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
        private readonly INamedTypeSymbol? _dateTimeSymbol = compilation.GetSpecialType(SpecialType.System_DateTime);
        private readonly INamedTypeSymbol? _stringSymbol = compilation.GetSpecialType(SpecialType.System_String);
        private readonly INamedTypeSymbol? _nullableSymbol = compilation.GetSpecialType(SpecialType.System_Nullable_T);

        public List<CsvMethodDefinition> GetCsvMethodDefinitions(ImmutableArray<MethodDeclarationSyntax> methods)
        {
            var results = new List<CsvMethodDefinition>();

            if (_generateCsvParserAttributeSymbol == null || _csvHeaderNameAttributeSymbol == null || _csvDateFormatAttributeSymbol == null ||
                _sepReaderSymbol == null || _iAsyncEnumerableSymbol == null || _cancellationTokenSymbol == null || _dateTimeSymbol == null ||
                _stringSymbol == null  || _nullableSymbol == null)
            {
                Diag(Diagnostic.Create(DiagnosticDescriptors.EssentialTypesNotFound, methods[0].GetLocation()));
                return results;
            }

            foreach (var methodSyntax in methods)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                SemanticModel semanticModel = _compilation.GetSemanticModel(methodSyntax.SyntaxTree);
                if (semanticModel.GetDeclaredSymbol(methodSyntax, _cancellationToken) is not IMethodSymbol methodSymbol)
                {
                    continue;
                }

                if (!ValidateMethodSignature(methodSymbol, methodSyntax))
                {
                    continue;
                }

                var containingClassSymbol = methodSymbol.ContainingType;
                if (containingClassSymbol == null) continue;

                var returnType = methodSymbol.ReturnType as INamedTypeSymbol;

                if (returnType?.TypeArguments.FirstOrDefault() is not INamedTypeSymbol itemTypeSymbol || !SymbolEqualityComparer.Default.Equals(itemTypeSymbol, containingClassSymbol))
                {
                    Diag(Diagnostic.Create(DiagnosticDescriptors.InvalidReturnType, methodSyntax.ReturnType.GetLocation(), containingClassSymbol.Name));
                    continue;
                }

                var propertiesToParse = new List<CsvPropertyDefinition>();
                foreach (var member in itemTypeSymbol.GetMembers())
                {
                    if (member is not IPropertySymbol propertySymbol) continue;
                    if (propertySymbol.IsStatic || propertySymbol.SetMethod == null) continue; // Must be instance property with a setter/init

                    AttributeData? headerAttr = propertySymbol.GetAttributes().FirstOrDefault(ad =>
                        SymbolEqualityComparer.Default.Equals(ad.AttributeClass, _csvHeaderNameAttributeSymbol));

                    if (headerAttr == null || headerAttr.ConstructorArguments.Length == 0) continue;

                    string? headerName = headerAttr.ConstructorArguments[0].Value as string;
                    if (string.IsNullOrWhiteSpace(headerName))
                    {
                        Diag(Diagnostic.Create(DiagnosticDescriptors.InvalidHeaderName, propertySymbol.Locations.FirstOrDefault()!, propertySymbol.Name));
                        continue;
                    }

                    string? dateFormat = null;
                    bool isDateTime = SymbolEqualityComparer.Default.Equals(propertySymbol.Type.OriginalDefinition, _dateTimeSymbol) ||
                                      (propertySymbol.Type is INamedTypeSymbol ntsDateTime && ntsDateTime.IsGenericType &&
                                       SymbolEqualityComparer.Default.Equals(ntsDateTime.TypeArguments.FirstOrDefault()?.OriginalDefinition, _dateTimeSymbol) &&
                                       SymbolEqualityComparer.Default.Equals(ntsDateTime.OriginalDefinition, _nullableSymbol));

                    if (isDateTime)
                    {
                        AttributeData? dateFormatAttr = propertySymbol.GetAttributes().FirstOrDefault(ad =>
                            SymbolEqualityComparer.Default.Equals(ad.AttributeClass, _csvDateFormatAttributeSymbol));
                        if (dateFormatAttr == null || dateFormatAttr.ConstructorArguments.Length == 0 ||
                            string.IsNullOrWhiteSpace(dateFormatAttr.ConstructorArguments[0].Value as string))
                        {
                            Diag(Diagnostic.Create(DiagnosticDescriptors.MissingDateFormatAttribute, propertySymbol.Locations.FirstOrDefault()!, propertySymbol.Name));
                            continue; // Skip this property if DateTime and no format
                        }
                        dateFormat = dateFormatAttr.ConstructorArguments[0].Value as string;
                    }

                    bool isRequired = propertySymbol.IsRequired; // For 'required' keyword

                    // TODO: this is checking for both nullable value types and nullable reference types. Ensure this is what we want.
                    // Or delete if we never end up using it.
                    bool isNullableType = propertySymbol.Type.NullableAnnotation == NullableAnnotation.Annotated ||
                                          (propertySymbol.Type is INamedTypeSymbol nts && nts.IsGenericType &&
                                           SymbolEqualityComparer.Default.Equals(nts.OriginalDefinition, _nullableSymbol));

                    ITypeSymbol underlyingType = (isNullableType && propertySymbol.Type is INamedTypeSymbol ntsNullable && ntsNullable.IsGenericType)
                                               ? ntsNullable.TypeArguments[0]
                                               : propertySymbol.Type;

                    propertiesToParse.Add(new CsvPropertyDefinition(
                        propertySymbol.Name,
                        propertySymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        underlyingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        headerName!,
                        dateFormat,
                        isRequired,
                        isNullableType,
                        isDateTime,
                        SymbolEqualityComparer.Default.Equals(underlyingType.OriginalDefinition, _stringSymbol)
                    ));
                }

                results.Add(new CsvMethodDefinition(methodSymbol, containingClassSymbol, itemTypeSymbol, [.. propertiesToParse]));
            }
            return results;
        }

        private bool ValidateMethodSignature(IMethodSymbol methodSymbol, MethodDeclarationSyntax methodSyntax)
        {
            bool isValid = true;
            if (!methodSymbol.IsPartialDefinition || !methodSymbol.IsStatic)
            {
                Diag(Diagnostic.Create(DiagnosticDescriptors.MethodNotPartialStatic, methodSyntax.Identifier.GetLocation()));
                isValid = false;
            }

            if (methodSymbol.ReturnType is not INamedTypeSymbol returnType ||
                !SymbolEqualityComparer.Default.Equals(returnType.OriginalDefinition, _iAsyncEnumerableSymbol) ||
                returnType.TypeArguments.Length != 1)
            {
                Diag(Diagnostic.Create(DiagnosticDescriptors.InvalidReturnType, methodSyntax.ReturnType.GetLocation(), methodSymbol.ContainingType.Name));
                isValid = false;
            }

            if (methodSymbol.Parameters.Length != 2 ||
                !SymbolEqualityComparer.Default.Equals(methodSymbol.Parameters[0].Type, _sepReaderSymbol) ||
                !SymbolEqualityComparer.Default.Equals(methodSymbol.Parameters[1].Type, _cancellationTokenSymbol))
            {
                Diag(Diagnostic.Create(DiagnosticDescriptors.InvalidMethodParameters, methodSyntax.ParameterList.GetLocation()));
                isValid = false;
            }
            return isValid;
        }

        private void Diag(Diagnostic diagnostic) => _reportDiagnostic(diagnostic);

        internal record CsvMethodDefinition(
            IMethodSymbol MethodSymbol,
            INamedTypeSymbol ContainingClassSymbol,
            INamedTypeSymbol ItemTypeSymbol,
            ImmutableList<CsvPropertyDefinition> PropertiesToParse);

        internal record CsvPropertyDefinition(
            string Name,
            string FullTypeName, // e.g., "System.Nullable<System.Int32>"
            string UnderlyingTypeName, // e.g., "System.Int32"
            string HeaderName,
            string? DateFormat,
            bool IsRequiredMember,
            bool IsNullableType,
            bool IsDateTime,
            bool IsString);
    }
}
