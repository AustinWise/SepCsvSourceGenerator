using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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

                    // TODO attributes of method

                    var csvMethod = new CsvMethod()
                    {
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
                        csvClass ??= new CsvClass();

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

    private void Diag(DiagnosticDescriptor desc, Location? location, params object?[]? messageArgs)
    {
        _reportDiagnostic(Diagnostic.Create(desc, location, messageArgs));
    }
}
