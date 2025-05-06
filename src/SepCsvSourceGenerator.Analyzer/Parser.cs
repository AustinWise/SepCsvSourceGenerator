using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace SepCsvSourceGenerator.Analyzer;

internal sealed class Parser
{
    public const string GenerateCsvParserAttribute = "SepCsvSourceGenerator.GenerateCsvParserAttribute";

    private readonly Compilation _compilation;
    private readonly Action<Diagnostic> _reportDiagnostic;
    private readonly CancellationToken _cancellationToken;

    public Parser(Compilation compilation, Action<Diagnostic> reportDiagnostic, CancellationToken cancellationToken)
    {
        _compilation = compilation;
        _reportDiagnostic = reportDiagnostic;
        _cancellationToken = cancellationToken;
    }

    public IReadOnlyList<CsvClass> GetCsvClasses(IEnumerable<ClassDeclarationSyntax> classes)
    {
        // TODO: logger message generator does some more probing for attributes. See the method GetBestTypeByMetadataName here:
        //   https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/Roslyn/GetBestTypeByMetadataName.cs
        // I don't know if that us really needed. Maybe it has something to do with the in-box / nuget nature of these attributes
        // as shipped with the shared framework?
        INamedTypeSymbol? generateCsvParserAttribute = _compilation.GetTypeByMetadataName(GenerateCsvParserAttribute);
        if (generateCsvParserAttribute == null)
        {
            return Array.Empty<CsvClass>();
        }

        INamedTypeSymbol? csvHeaderNameAttribute = _compilation.GetTypeByMetadataName("SepCsvSourceGenerator.CsvHeaderNameAttribute");
        if (csvHeaderNameAttribute == null)
        {
            return Array.Empty<CsvClass>();
        }

        var ret = new List<CsvClass>();

        foreach (IGrouping<SyntaxTree, ClassDeclarationSyntax> group in classes.GroupBy(x => x.SyntaxTree))
        {
            SyntaxTree syntaxTree = group.Key;
            SemanticModel sm = _compilation.GetSemanticModel(syntaxTree);

            foreach (ClassDeclarationSyntax classDec in group)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                CsvClass? csvClass = null;

                foreach (MemberDeclarationSyntax member in classDec.Members)
                {
                    var method = member as MethodDeclarationSyntax;
                    if (method == null)
                    {
                        continue;
                    }

                    bool includeFields = false;

                    IMethodSymbol csvMethodSymbol = sm.GetDeclaredSymbol(method, _cancellationToken)!;
                    if (!HasCsvGenAttribute(sm, generateCsvParserAttribute, method))
                    {
                        continue;
                    }

                    bool hasMisconfiguredInput = false;
                    ImmutableArray<AttributeData> boundAttributes = csvMethodSymbol.GetAttributes();

                    if (boundAttributes.Length == 0)
                    {
                        continue;
                    }

                    foreach (AttributeData attributeData in boundAttributes)
                    {
                        if (SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, generateCsvParserAttribute))
                        {
                            foreach (KeyValuePair<string, TypedConstant> namedArgument in attributeData.NamedArguments)
                            {
                                TypedConstant typedConstant = namedArgument.Value;
                                if (typedConstant.Kind == TypedConstantKind.Error)
                                {
                                    hasMisconfiguredInput = true;
                                    break; // if a compilation error was found, no need to keep evaluating other args
                                }
                                else
                                {
                                    TypedConstant value = namedArgument.Value;
                                    switch (namedArgument.Key)
                                    {
                                        case "IncludeFields":
                                            includeFields = (bool)value.Value!;
                                            break;
                                    }
                                }
                            }
                        }
                    }

                    if (hasMisconfiguredInput)
                    {
                        // skip further generator execution and let compiler generate the errors
                        break;
                    }


                    var csvMethod = new CsvMethod()
                    {
                        IncludeFields = includeFields,
                    };

                    bool keepMethod = true;


                    bool isStatic = false;
                    bool isPartial = false;
                    foreach (SyntaxToken mod in method.Modifiers)
                    {
                        if (mod.IsKind(SyntaxKind.PartialKeyword))
                        {
                            isPartial = true;
                        }
                        else if (mod.IsKind(SyntaxKind.StaticKeyword))
                        {
                            isStatic = true;
                        }
                    }

                    if (!isPartial)
                    {
                        Diag(DiagnosticDescriptors.CsvParsingMethodMustBePartial, method.GetLocation());
                        keepMethod = false;
                    }

                    if (!isStatic)
                    {
                        Diag(DiagnosticDescriptors.CsvParsingMethodMustBeStatic, method.GetLocation());
                        keepMethod = false;
                    }

                    if (keepMethod)
                    {
                        csvClass ??= new CsvClass()
                        {
                        };

                        csvClass.Methods.Add(csvMethod);
                    }
                }

                if (csvClass is not null)
                {
                    ret.Add(csvClass);
                }
            }
        }

        return ret;
    }

    private bool HasCsvGenAttribute(SemanticModel sm, INamedTypeSymbol generateCsvParserAttribute, MethodDeclarationSyntax method)
    {
        foreach (AttributeListSyntax mal in method.AttributeLists)
        {
            foreach (AttributeSyntax ma in mal.Attributes)
            {
                IMethodSymbol? attrCtorSymbol = sm.GetSymbolInfo(ma, _cancellationToken).Symbol as IMethodSymbol;
                if (attrCtorSymbol != null && generateCsvParserAttribute.Equals(attrCtorSymbol.ContainingType, SymbolEqualityComparer.Default))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void Diag(DiagnosticDescriptor desc, Location? location, params object?[]? messageArgs)
    {
        _reportDiagnostic(Diagnostic.Create(desc, location, messageArgs));
    }
}
