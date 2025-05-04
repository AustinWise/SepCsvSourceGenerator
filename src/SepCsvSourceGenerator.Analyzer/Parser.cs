using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace SepCsvSourceGenerator.Analyzer;

internal sealed class Parser
{
    public const string GenerateCsvParserAttribute = "SepCsvSourceGenerator.GenerateCsvParserAttribute";

    private readonly CancellationToken _cancellationToken;
    private readonly Compilation _compilation;
    private readonly Action<Diagnostic> _reportDiagnostic;

    public Parser(Compilation compilation, Action<Diagnostic> reportDiagnostic, CancellationToken cancellationToken)
    {
        _compilation = compilation;
        _cancellationToken = cancellationToken;
        _reportDiagnostic = reportDiagnostic;
    }

    public IReadOnlyList<CsvClass> GetCsvClasses(IEnumerable<ClassDeclarationSyntax> classes)
    {
        throw new NotImplementedException();
    }
}
