using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
    private readonly INamedTypeSymbol? _sepReaderRowSymbol = compilation.GetTypeByMetadataName("nietras.SeparatedValues.SepReader+Row");
    private readonly INamedTypeSymbol? _sepReaderHeaderSymbol = compilation.GetTypeByMetadataName("nietras.SeparatedValues.SepReaderHeader");
    private readonly INamedTypeSymbol? _iAsyncEnumerableSymbol = compilation.GetTypeByMetadataName("System.Collections.Generic.IAsyncEnumerable`1");
    private readonly INamedTypeSymbol? _iEnumerableSymbol = compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");
    private readonly INamedTypeSymbol? _listSymbol = compilation.GetTypeByMetadataName("System.Collections.Generic.List`1");
    private readonly INamedTypeSymbol? _cancellationTokenSymbol = compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
    private readonly INamedTypeSymbol? _dateTimeSymbol = compilation.GetSpecialType(SpecialType.System_DateTime);
    private readonly INamedTypeSymbol? _dateTimeOffsetSymbol = compilation.GetTypeByMetadataName("System.DateTimeOffset");
    private readonly INamedTypeSymbol? _dateOnlySymbol = compilation.GetTypeByMetadataName("System.DateOnly");
    private readonly INamedTypeSymbol? _timeOnlySymbol = compilation.GetTypeByMetadataName("System.TimeOnly");
    private readonly INamedTypeSymbol? _stringSymbol = compilation.GetSpecialType(SpecialType.System_String);
    private readonly INamedTypeSymbol? _nullableSymbol = compilation.GetSpecialType(SpecialType.System_Nullable_T);
    private readonly INamedTypeSymbol? _enumSymbol = compilation.GetTypeByMetadataName("System.Enum");
    private readonly INamedTypeSymbol? _iSpanParsableSymbol = compilation.GetTypeByMetadataName("System.ISpanParsable`1");

    private INamedTypeSymbol? _iEnumerableSymbolOfRow;
    private INamedTypeSymbol IEnumerableSymbolOfRow
    {
        get
        {
            if (_iEnumerableSymbolOfRow != null)
                return _iEnumerableSymbolOfRow;
            return _iEnumerableSymbolOfRow = _iEnumerableSymbol!.Construct(_sepReaderRowSymbol!);
        }
    }

    private INamedTypeSymbol? _iAsyncEnumerableSymbolOfRow;
    private INamedTypeSymbol IAsyncEnumerableSymbolOfRow
    {
        get
        {
            if (_iAsyncEnumerableSymbolOfRow != null)
                return _iAsyncEnumerableSymbolOfRow;
            return _iAsyncEnumerableSymbolOfRow = _iAsyncEnumerableSymbol!.Construct(_sepReaderRowSymbol!);
        }
    }

    private static string reformatFieldName(string fieldName)
    {
        if (fieldName == nameof(_sepReaderRowSymbol))
        {
            return "SepReader.Row";
        }
        fieldName = fieldName.Remove(fieldName.Length - "Symbol".Length);
        fieldName = char.ToUpperInvariant(fieldName[1]) + fieldName.Substring(2);
        return fieldName;
    }

    public List<CsvMethodDefinition> GetCsvMethodDefinitions(ImmutableArray<MethodDeclarationSyntax> methods)
    {
        var results = new List<CsvMethodDefinition>();

        List<FieldInfo> nullFields = [.. this.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Where(f => f.Name != nameof(_iEnumerableSymbolOfRow) && f.Name != nameof(_iAsyncEnumerableSymbolOfRow) && f.FieldType == typeof(INamedTypeSymbol) && f.GetValue(this) is null)];
        if (nullFields.Count != 0)
        {
            string missingTypes = string.Join(", ", nullFields.Select(f => reformatFieldName(f.Name)).OrderBy(f => f));
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

            if (!ValidateMethodSignature(methodSymbol, methodSyntax, out bool isAsync, out string? readerParameterName, out string? headersParameterName, out string? ctParameterName))
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

                    string[] headerNames;
                    if (headerAttr is not null)
                    {
                        if (headerAttr.ConstructorArguments.Length == 1 &&
                            headerAttr.ConstructorArguments[0].Kind == TypedConstantKind.Array)
                        {
                            var values = headerAttr.ConstructorArguments[0].Values;
                            if (values.IsDefaultOrEmpty)
                            {
                                Diag(Diagnostic.Create(DiagnosticDescriptors.HeaderNamesEmpty, propertySymbol.Locations.FirstOrDefault()!, propertySymbol.Name));
                                continue;
                            }
                            headerNames = [.. values.Select(c => (string)c.Value!)];
                        }
                        else
                        {
                            Debug.Fail("There should be no non-array constructor for this attribute.");
                            continue;
                        }
                    }
                    else
                    {
                        headerNames = [propertySymbol.Name];
                    }

                    if (headerNames.Any(string.IsNullOrWhiteSpace))
                    {
                        Diag(Diagnostic.Create(DiagnosticDescriptors.InvalidHeaderName, propertySymbol.Locations.FirstOrDefault()!, propertySymbol.Name));
                        continue;
                    }

                    string? dateFormat = null;
                    string? elementTypeName = null;
                    CsvPropertyKind? elementKind = null;
                    string? elementDateFormat = null;
                    char listDelimiter = ',';
                    var kind = CsvPropertyKind.SpanParsable;

                    bool isNullableType = IsNullableType(propertySymbol.Type, out ITypeSymbol underlyingType);

                    // Check if this is a list type (array or List<T>)
                    bool isListType = IsListType(underlyingType, out ITypeSymbol elementType);

                    ITypeSymbol typeToAnalyze = isListType ? elementType : underlyingType;

                    if (isListType)
                    {
                        kind = CsvPropertyKind.List;
                        elementTypeName = elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                        // Determine the kind of the element type
                        var (elemKind, elemDateFmt) = DetermineTypeKind(elementType, propertySymbol, true);
                        
                        if (elemKind == CsvPropertyKind.DateOrTime && elemDateFmt == null)
                        {
                            Diag(Diagnostic.Create(DiagnosticDescriptors.MissingDateFormatAttribute, propertySymbol.Locations.FirstOrDefault()!, propertySymbol.Name));
                            continue;
                        }
                        
                        elementKind = elemKind;
                        elementDateFormat = elemDateFmt;

                        // Validate that element type is parsable
                        if (elemKind == CsvPropertyKind.SpanParsable)
                        {
                            bool isSpanParsable = elementType.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, _iSpanParsableSymbol));
                            if (!isSpanParsable && elementType is ITypeParameterSymbol typeParameter)
                            {
                                isSpanParsable = typeParameter.ConstraintTypes.SelectMany(t => t.AllInterfaces.Concat([t.OriginalDefinition as INamedTypeSymbol])).Any(i => SymbolEqualityComparer.Default.Equals(i?.OriginalDefinition, _iSpanParsableSymbol));
                            }

                            if (!isSpanParsable)
                            {
                                Diag(Diagnostic.Create(DiagnosticDescriptors.PropertyNotParsable, propertySymbol.Locations.FirstOrDefault()!, propertySymbol.Name, elementType.Name));
                                continue;
                            }
                        }
                    }
                    else
                    {
                        // Not a list type - handle as scalar
                        var (scalarKind, scalarDateFmt) = DetermineTypeKind(typeToAnalyze, propertySymbol, true);
                        kind = scalarKind;
                        dateFormat = scalarDateFmt;

                        if (kind == CsvPropertyKind.DateOrTime && dateFormat == null)
                        {
                            Diag(Diagnostic.Create(DiagnosticDescriptors.MissingDateFormatAttribute, propertySymbol.Locations.FirstOrDefault()!, propertySymbol.Name));
                            continue;
                        }

                        if (kind == CsvPropertyKind.SpanParsable)
                        {
                            bool isSpanParsable = typeToAnalyze.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, _iSpanParsableSymbol));
                            if (!isSpanParsable && typeToAnalyze is ITypeParameterSymbol typeParameter)
                            {
                                isSpanParsable = typeParameter.ConstraintTypes.SelectMany(t => t.AllInterfaces.Concat([t.OriginalDefinition as INamedTypeSymbol])).Any(i => SymbolEqualityComparer.Default.Equals(i?.OriginalDefinition, _iSpanParsableSymbol));
                            }

                            if (!isSpanParsable)
                            {
                                Diag(Diagnostic.Create(DiagnosticDescriptors.PropertyNotParsable, propertySymbol.Locations.FirstOrDefault()!, propertySymbol.Name, typeToAnalyze.Name));
                                continue;
                            }
                        }
                    }

                    propertiesToParse.Add(new CsvPropertyDefinition(
                        propertySymbol.Name,
                        propertySymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        underlyingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        headerNames,
                        dateFormat,
                        propertySymbol.IsRequired,
                        propertySymbol.SetMethod?.IsInitOnly ?? false,
                        kind,
                        elementTypeName,
                        elementKind,
                        elementDateFormat,
                        listDelimiter
                    ));
                }
                currentType = currentType.BaseType;
            }

            if (seenProperties.Count == 0)
            {
                // We did not find any any properties to parse.
                // That's was probably a mistake.
                Diag(Diagnostic.Create(DiagnosticDescriptors.NoPropertiesFound, methodSyntax.Identifier.GetLocation(), itemTypeSymbol.Name));
            }

            var readerParameterType = (INamedTypeSymbol)methodSymbol.Parameters[0].Type;

            results.Add(new CsvMethodDefinition(methodSymbol, containingClassSymbol, itemTypeSymbol, readerParameterType, isAsync, readerParameterName, headersParameterName, ctParameterName, [.. propertiesToParse]));
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

    private bool IsListType(ITypeSymbol type, out ITypeSymbol elementType)
    {
        // Check for arrays (e.g., string[], int[])
        if (type is IArrayTypeSymbol arrayType)
        {
            elementType = arrayType.ElementType;
            return true;
        }
        
        // Check for List<T>
        if (type is INamedTypeSymbol namedType && 
            namedType.IsGenericType && 
            SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, _listSymbol))
        {
            elementType = namedType.TypeArguments[0];
            return true;
        }
        
        elementType = null!;
        return false;
    }

    private (CsvPropertyKind kind, string? dateFormat) DetermineTypeKind(ITypeSymbol type, IPropertySymbol propertySymbol, bool allowDateFormat)
    {
        string? dateFormat = null;
        var kind = CsvPropertyKind.SpanParsable;

        bool isDateOrTime = SymbolEqualityComparer.Default.Equals(type, _dateTimeSymbol) ||
                            SymbolEqualityComparer.Default.Equals(type, _dateTimeOffsetSymbol) ||
                            SymbolEqualityComparer.Default.Equals(type, _dateOnlySymbol) ||
                            SymbolEqualityComparer.Default.Equals(type, _timeOnlySymbol);

        if (isDateOrTime)
        {
            kind = CsvPropertyKind.DateOrTime;
            if (allowDateFormat)
            {
                AttributeData? dateFormatAttr = propertySymbol.GetAttributes().FirstOrDefault(ad =>
                    SymbolEqualityComparer.Default.Equals(ad.AttributeClass, _csvDateFormatAttributeSymbol));
                if (dateFormatAttr != null && dateFormatAttr.ConstructorArguments.Length > 0 &&
                    !string.IsNullOrWhiteSpace(dateFormatAttr.ConstructorArguments[0].Value as string))
                {
                    dateFormat = dateFormatAttr.ConstructorArguments[0].Value as string;
                }
                // If date format not found, dateFormat remains null, and kind is DateOrTime
                // The caller will check for this and emit a diagnostic
            }
        }
        else if (type.BaseType != null && SymbolEqualityComparer.Default.Equals(type.BaseType, _enumSymbol))
        {
            kind = CsvPropertyKind.Enum;
        }
        else if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, _stringSymbol))
        {
            kind = CsvPropertyKind.String;
        }

        return (kind, dateFormat);
    }

    private bool ValidateMethodSignature(IMethodSymbol methodSymbol, MethodDeclarationSyntax methodSyntax, out bool isAsync, [NotNullWhen(true)] out string? readerParameterName, out string? headersParameterName, out string? ctParameterName)
    {
        isAsync = false;
        headersParameterName = null;
        ctParameterName = null;
        readerParameterName = null;

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

        bool readerIsEnumerable = false; // true if IAsyncEnumerable or IEnumerable, false if SepReader

        // TODO: all these diags should be more precise.
        foreach (var parameter in methodSymbol.Parameters)
        {
            if (SymbolEqualityComparer.Default.Equals(parameter.Type, _cancellationTokenSymbol))
            {
                if (ctParameterName is not null)
                {
                    Diag(Diagnostic.Create(DiagnosticDescriptors.DuplicateCancellationTokenParameter, parameter.Locations[0]));
                    isValid = false;
                }
                ctParameterName = parameter.Name;
            }
            else if (SymbolEqualityComparer.Default.Equals(parameter.Type, _sepReaderHeaderSymbol))
            {
                if (headersParameterName is not null)
                {
                    Diag(Diagnostic.Create(DiagnosticDescriptors.DuplicateHeaderParameter, parameter.Locations[0]));
                    isValid = false;
                }
                headersParameterName = parameter.Name;
            }
            else if (SymbolEqualityComparer.Default.Equals(parameter.Type, _sepReaderSymbol))
            {
                if (readerParameterName is not null)
                {
                    Diag(Diagnostic.Create(DiagnosticDescriptors.DuplicateReaderParameter, parameter.Locations[0]));
                    isValid = false;
                }
                readerParameterName = parameter.Name;
            }
            else if (SymbolEqualityComparer.Default.Equals(parameter.Type, IEnumerableSymbolOfRow))
            {
                if (isAsync)
                {
                    Diag(Diagnostic.Create(DiagnosticDescriptors.UnexpectedIEnumerableParameter, parameter.Locations[0]));
                    isValid = false;
                }
                if (readerParameterName is not null)
                {
                    Diag(Diagnostic.Create(DiagnosticDescriptors.DuplicateReaderParameter, parameter.Locations[0]));
                    isValid = false;
                }
                readerIsEnumerable = true;
                readerParameterName = parameter.Name;
            }
            else if (SymbolEqualityComparer.Default.Equals(parameter.Type, IAsyncEnumerableSymbolOfRow))
            {
                if (!isAsync)
                {
                    Diag(Diagnostic.Create(DiagnosticDescriptors.UnexpectedIAsyncEnumerableParameter, parameter.Locations[0]));
                    isValid = false;
                }
                if (readerParameterName is not null)
                {
                    Diag(Diagnostic.Create(DiagnosticDescriptors.DuplicateReaderParameter, parameter.Locations[0]));
                    isValid = false;
                }
                readerIsEnumerable = true;
                readerParameterName = parameter.Name;
            }
            else
            {
                Diag(Diagnostic.Create(DiagnosticDescriptors.UnexpectedParameterType, parameter.Locations[0]));
                isValid = false;
            }
        }

        if (readerParameterName is null)
        {
            Diag(Diagnostic.Create(DiagnosticDescriptors.MissingReaderParameter, methodSyntax.ParameterList.GetLocation()));
            isValid = false;
        }

        if (readerIsEnumerable && headersParameterName is null)
        {
            Diag(Diagnostic.Create(DiagnosticDescriptors.MissingHeaderParameter, methodSyntax.ParameterList.GetLocation()));
            isValid = false;
        }


        return isValid;

    }

    private void Diag(Diagnostic diagnostic) => _reportDiagnostic(diagnostic);
}
