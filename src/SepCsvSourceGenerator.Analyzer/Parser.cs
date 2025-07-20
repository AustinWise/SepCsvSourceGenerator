using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;

namespace AWise.SepCsvSourceGenerator.Analyzer;

internal sealed class Parser(Compilation compilation, Action<Diagnostic> reportDiagnostic, CancellationToken cancellationToken)
{
    const string GeneratorNamespace = "AWise.SepCsvSourceGenerator";
    public const string GenerateCsvParserAttributeFullName = GeneratorNamespace + ".GenerateCsvParserAttribute";

    private readonly Compilation _compilation = compilation;
    private readonly Action<Diagnostic> _reportDiagnostic = reportDiagnostic;
    private readonly CancellationToken _cancellationToken = cancellationToken;

    private readonly INamedTypeSymbol? _generateCsvParserAttributeSymbol = compilation.GetTypeByMetadataName(GenerateCsvParserAttributeFullName);
    private readonly INamedTypeSymbol? _csvHeaderNameAttributeSymbol = compilation.GetTypeByMetadataName(GeneratorNamespace + ".CsvHeaderNameAttribute");
    private readonly INamedTypeSymbol? _csvDateFormatAttributeSymbol = compilation.GetTypeByMetadataName(GeneratorNamespace + ".CsvDateFormatAttribute");
    private readonly INamedTypeSymbol? _sepReaderSymbol = compilation.GetTypeByMetadataName("nietras.SeparatedValues.SepReader");
    private readonly INamedTypeSymbol? _iAsyncEnumerableSymbol = compilation.GetTypeByMetadataName("System.Collections.Generic.IAsyncEnumerable`1");
    private readonly INamedTypeSymbol? _iEnumerableSymbol = compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");
    private readonly INamedTypeSymbol? _cancellationTokenSymbol = compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
    private readonly INamedTypeSymbol? _dateTimeSymbol = compilation.GetSpecialType(SpecialType.System_DateTime);
    private readonly INamedTypeSymbol? _dateTimeOffsetSymbol = compilation.GetTypeByMetadataName("System.DateTimeOffset");
    private readonly INamedTypeSymbol? _dateOnlySymbol = compilation.GetTypeByMetadataName("System.DateOnly");
    private readonly INamedTypeSymbol? _timeOnlySymbol = compilation.GetTypeByMetadataName("System.TimeOnly");
    private readonly INamedTypeSymbol? _stringSymbol = compilation.GetSpecialType(SpecialType.System_String);
    private readonly INamedTypeSymbol? _nullableSymbol = compilation.GetSpecialType(SpecialType.System_Nullable_T);
    private readonly INamedTypeSymbol? _enumSymbol = compilation.GetTypeByMetadataName("System.Enum");
    private readonly INamedTypeSymbol? _iSpanParsableSymbol = compilation.GetTypeByMetadataName("System.ISpanParsable`1");

    private static string reformatFieldName(string fieldName)
    {
        fieldName = fieldName.Remove(fieldName.Length - "Symbol".Length);
        fieldName = char.ToUpperInvariant(fieldName[1]) + fieldName.Substring(2);
        return fieldName;
    }

    public List<CsvMethodDefinition> GetCsvMethodDefinitions(ImmutableArray<MethodDeclarationSyntax> methods)
    {
        var results = new List<CsvMethodDefinition>();

        List<FieldInfo> nullFields = [.. this.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Where(f => f.FieldType == typeof(INamedTypeSymbol) && f.GetValue(this) is null)];
        if (nullFields.Count != 0)
        {
            string missingTypes = string.Join(", ", nullFields.Select(f => reformatFieldName(f.Name)));
            Diag(Diagnostic.Create(DiagnosticDescriptors.EssentialTypesNotFound, methods[0].GetLocation(), missingTypes));
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

            AttributeData? generateCsvAttr = methodSymbol.GetAttributes().Where(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, _generateCsvParserAttributeSymbol)).FirstOrDefault();
            if (generateCsvAttr == null)
            {
                // TODO: throw an exception or log a diagnostic? This should not happen.
                Debug.Fail("Method marked with [GenerateCsvParser] should not be processed here.");
                continue;
            }

            if (!ValidateMethodSignature(methodSymbol, methodSyntax, out bool isAsync))
            {
                continue;
            }

            var containingClassSymbol = methodSymbol.ContainingType;
            if (containingClassSymbol == null) continue;

            var returnType = methodSymbol.ReturnType as INamedTypeSymbol;

            if (returnType?.TypeArguments.FirstOrDefault() is not INamedTypeSymbol itemTypeSymbol)
            {
                Diag(Diagnostic.Create(DiagnosticDescriptors.InvalidReturnType, methodSyntax.ReturnType.GetLocation(), containingClassSymbol.Name));
                continue;
            }

            bool includeAllProperties = false;
            foreach (var arg in generateCsvAttr.NamedArguments)
            {
                if (arg.Key == "IncludeProperties")
                {
                    if (arg.Value.Kind == TypedConstantKind.Primitive && arg.Value.Value is bool b)
                    {
                        includeAllProperties = b;
                    }
                    else
                    {
                        Debug.Fail("Wrong type?");
                    }
                }
                else
                {
                    Debug.Fail("Unexpected named argument on GenerateCsvParser: " + arg.Key);
                }
            }

            var propertiesToParse = new List<CsvPropertyDefinition>();
            var currentType = itemTypeSymbol;
            var seenProperties = new HashSet<string>();

            while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
            {
                foreach (var member in currentType.GetMembers())
                {
                    if (member is not IPropertySymbol propertySymbol) continue;
                    if (propertySymbol.IsStatic || propertySymbol.SetMethod == null) continue; // Must be instance property with a setter/init
                    AttributeData? headerAttr = propertySymbol.GetAttributes().FirstOrDefault(ad =>
                        SymbolEqualityComparer.Default.Equals(ad.AttributeClass, _csvHeaderNameAttributeSymbol));
                    if (headerAttr is null && !includeAllProperties) continue;
                    if (!seenProperties.Add(propertySymbol.Name)) continue; // Property already seen in a more derived type

                    // From here on, if we use "continue" we must raise a diagnostic.
                    // This ensures we will either get NoPropertiesFound or some other diagnostic if something is wrong.

                    string? headerName = propertySymbol.Name;
                    if (headerAttr != null && headerAttr.ConstructorArguments.Length == 1)
                    {
                        headerName = headerAttr.ConstructorArguments[0].Value as string;
                    }

                    if (string.IsNullOrWhiteSpace(headerName))
                    {
                        Diag(Diagnostic.Create(DiagnosticDescriptors.InvalidHeaderName, propertySymbol.Locations.FirstOrDefault()!, propertySymbol.Name));
                        continue;
                    }

                    string? dateFormat = null;
                    var kind = CsvPropertyKind.SpanParsable;

                    bool isNullableType = IsNullableType(propertySymbol.Type, out ITypeSymbol underlyingType);

                    bool isDateOrTime = SymbolEqualityComparer.Default.Equals(underlyingType, _dateTimeSymbol) ||
                                        SymbolEqualityComparer.Default.Equals(underlyingType, _dateTimeOffsetSymbol) ||
                                        SymbolEqualityComparer.Default.Equals(underlyingType, _dateOnlySymbol) ||
                                        SymbolEqualityComparer.Default.Equals(underlyingType, _timeOnlySymbol);

                    if (isDateOrTime)
                    {
                        kind = CsvPropertyKind.DateOrTime;
                        AttributeData? dateFormatAttr = propertySymbol.GetAttributes().FirstOrDefault(ad =>
                            SymbolEqualityComparer.Default.Equals(ad.AttributeClass, _csvDateFormatAttributeSymbol));
                        if (dateFormatAttr == null || dateFormatAttr.ConstructorArguments.Length == 0 ||
                            string.IsNullOrWhiteSpace(dateFormatAttr.ConstructorArguments[0].Value as string))
                        {
                            Diag(Diagnostic.Create(DiagnosticDescriptors.MissingDateFormatAttribute, propertySymbol.Locations.FirstOrDefault()!, propertySymbol.Name));
                            continue;
                        }
                        dateFormat = dateFormatAttr.ConstructorArguments[0].Value as string;
                    }
                    else if (underlyingType.BaseType != null && SymbolEqualityComparer.Default.Equals(underlyingType.BaseType, _enumSymbol))
                    {
                        kind = CsvPropertyKind.Enum;
                    }
                    else if (SymbolEqualityComparer.Default.Equals(underlyingType.OriginalDefinition, _stringSymbol))
                    {
                        kind = CsvPropertyKind.String;
                    }

                    if (kind == CsvPropertyKind.SpanParsable)
                    {
                        bool isSpanParsable = underlyingType.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, _iSpanParsableSymbol));
                        if (!isSpanParsable && underlyingType is ITypeParameterSymbol typeParameter)
                        {
                            isSpanParsable = typeParameter.ConstraintTypes.SelectMany(t => t.AllInterfaces.Concat([t.OriginalDefinition as INamedTypeSymbol])).Any(i => SymbolEqualityComparer.Default.Equals(i?.OriginalDefinition, _iSpanParsableSymbol));
                        }

                        if (!isSpanParsable)
                        {
                            Diag(Diagnostic.Create(DiagnosticDescriptors.PropertyNotParsable, propertySymbol.Locations.FirstOrDefault()!, propertySymbol.Name, underlyingType.Name));
                            continue;
                        }
                    }

                    propertiesToParse.Add(new CsvPropertyDefinition(
                        propertySymbol.Name,
                        propertySymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        underlyingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        headerName!,
                        dateFormat,
                        propertySymbol.IsRequired,
                        propertySymbol.SetMethod?.IsInitOnly ?? false,
                        kind
                    ));
                }
                currentType = currentType.BaseType;
            }

            if (seenProperties.Count == 0)
            {
                // We did not see any properties marked with the CsvHeaderName attribute.
                // That's was probably a mistake.
                Diag(Diagnostic.Create(DiagnosticDescriptors.NoPropertiesFound, methodSyntax.Identifier.GetLocation(), itemTypeSymbol.Name));
                continue;
            }

            if (propertiesToParse.Count == 0)
            {
                // If we saw property but it had something wrong with it, we should have already raised a diagnostic.
                // So we silently continue.
                continue;
            }

            results.Add(new CsvMethodDefinition(methodSymbol, containingClassSymbol, itemTypeSymbol, isAsync, [.. propertiesToParse]));
        }
        return results;
    }

    private bool IsNullableType(ITypeSymbol type, out ITypeSymbol underlyingType)
    {
        if (type is INamedTypeSymbol nts && nts.IsGenericType && SymbolEqualityComparer.Default.Equals(nts.OriginalDefinition, _nullableSymbol))
        {
            underlyingType = nts.TypeArguments[0];
            return true;
        }
        underlyingType = type;
        return false;
    }

    private bool ValidateMethodSignature(IMethodSymbol methodSymbol, MethodDeclarationSyntax methodSyntax, out bool isAsync)
    {
        isAsync = false;
        bool isValid = true;
        if (!methodSymbol.IsPartialDefinition)
        {
            Diag(Diagnostic.Create(DiagnosticDescriptors.MethodNotPartial, methodSyntax.Identifier.GetLocation(), methodSymbol.Name));
            isValid = false;
        }

        if (methodSymbol.ReturnType is not INamedTypeSymbol returnType ||
            returnType.TypeArguments.Length != 1)
        {
            Diag(Diagnostic.Create(DiagnosticDescriptors.InvalidReturnType, methodSyntax.ReturnType.GetLocation(), methodSymbol.ContainingType.Name));
            return false;
        }

        bool isAsyncEnumerable = SymbolEqualityComparer.Default.Equals(returnType.OriginalDefinition, _iAsyncEnumerableSymbol);
        bool isEnumerable = SymbolEqualityComparer.Default.Equals(returnType.OriginalDefinition, _iEnumerableSymbol);

        if (!isAsyncEnumerable && !isEnumerable)
        {
            Diag(Diagnostic.Create(DiagnosticDescriptors.InvalidReturnType, methodSyntax.ReturnType.GetLocation(), methodSymbol.ContainingType.Name));
            isValid = false;
        }

        if (isAsyncEnumerable)
        {
            isAsync = true;
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
}
