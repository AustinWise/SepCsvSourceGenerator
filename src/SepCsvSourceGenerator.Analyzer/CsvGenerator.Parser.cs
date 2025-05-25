using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace SepCsvSourceGenerator;

public partial class CsvGenerator
{
    internal sealed class Parser
    {
        private readonly Compilation _compilation;
        private readonly Action<Diagnostic> _reportDiagnostic;
        private readonly CancellationToken _cancellationToken;

        private readonly INamedTypeSymbol? _generateCsvParserAttributeSymbol;
        private readonly INamedTypeSymbol? _csvHeaderNameAttributeSymbol;
        private readonly INamedTypeSymbol? _csvDateFormatAttributeSymbol;
        private readonly INamedTypeSymbol? _sepReaderSymbol;
        private readonly INamedTypeSymbol? _iAsyncEnumerableSymbol;
        private readonly INamedTypeSymbol? _cancellationTokenSymbol;
        private readonly INamedTypeSymbol? _dateTimeSymbol;
        private readonly INamedTypeSymbol? _stringSymbol;
        private readonly INamedTypeSymbol? _nullableSymbol;

        public Parser(Compilation compilation, Action<Diagnostic> reportDiagnostic, CancellationToken cancellationToken)
        {
            _compilation = compilation;
            _reportDiagnostic = reportDiagnostic;
            _cancellationToken = cancellationToken;

            _generateCsvParserAttributeSymbol = compilation.GetTypeByMetadataName(GenerateCsvParserAttributeFullName);
            _csvHeaderNameAttributeSymbol = compilation.GetTypeByMetadataName("SepCsvSourceGenerator.CsvHeaderNameAttribute");
            _csvDateFormatAttributeSymbol = compilation.GetTypeByMetadataName("SepCsvSourceGenerator.CsvDateFormatAttribute");

            _sepReaderSymbol = compilation.GetTypeByMetadataName("nietras.SeparatedValues.SepReader");
            _iAsyncEnumerableSymbol = compilation.GetTypeByMetadataName("System.Collections.Generic.IAsyncEnumerable`1");
            _cancellationTokenSymbol = compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
            _dateTimeSymbol = compilation.GetSpecialType(SpecialType.System_DateTime);
            _stringSymbol = compilation.GetSpecialType(SpecialType.System_String);
            _nullableSymbol = compilation.GetSpecialType(SpecialType.System_Nullable_T);
        }

        public List<CsvMethodDefinition> GetCsvMethodDefinitions(ImmutableArray<MethodDeclarationSyntax> methods)
        {
            var results = new List<CsvMethodDefinition>();

            if (_generateCsvParserAttributeSymbol == null || _csvHeaderNameAttributeSymbol == null || _csvDateFormatAttributeSymbol == null ||
                _sepReaderSymbol == null || _iAsyncEnumerableSymbol == null || _cancellationTokenSymbol == null || _dateTimeSymbol == null || _stringSymbol == null)
            {
                // Report diagnostic: essential types not found
                // Diag(DiagnosticDescriptors.EssentialTypesNotFound, null); // Example
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
                var itemTypeSymbol = returnType?.TypeArguments.FirstOrDefault() as INamedTypeSymbol;

                if (itemTypeSymbol == null || !SymbolEqualityComparer.Default.Equals(itemTypeSymbol, containingClassSymbol))
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
                        // Diag(DiagnosticDescriptors.InvalidHeaderName, propertySymbol.Locations.FirstOrDefault(), propertySymbol.Name);
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

                results.Add(new CsvMethodDefinition(methodSymbol, containingClassSymbol, itemTypeSymbol, propertiesToParse.ToImmutableList()));
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

    // Define DiagnosticDescriptors (simplified for brevity)
    // In a real scenario, these would be more detailed and localized.
    internal static class DiagnosticDescriptors
    {
        public static readonly DiagnosticDescriptor MethodNotPartialStatic =
            new("CSVGEN001", "Method must be partial and static", "Method '{0}' must be declared as 'partial static'", "Usage", DiagnosticSeverity.Error, true);
        public static readonly DiagnosticDescriptor InvalidReturnType =
            new("CSVGEN002", "Invalid return type", "Method must return 'IAsyncEnumerable<{0}>'", "Usage", DiagnosticSeverity.Error, true);
        public static readonly DiagnosticDescriptor InvalidMethodParameters =
            new("CSVGEN003", "Invalid method parameters", "Method must have parameters '(SepReader reader, CancellationToken ct)'", "Usage", DiagnosticSeverity.Error, true);
        public static readonly DiagnosticDescriptor MissingDateFormatAttribute =
            new("CSVGEN004", "Missing CsvDateFormat attribute", "Property '{0}' of type DateTime or DateTime? must have a [CsvDateFormat] attribute", "Usage", DiagnosticSeverity.Error, true);
    }
}
