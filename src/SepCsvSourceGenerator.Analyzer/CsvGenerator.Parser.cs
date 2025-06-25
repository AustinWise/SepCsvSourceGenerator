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
                // TODO: Analyze method and containing class, build CsvParseTarget
            }
            return result;
        }

        internal class CsvParseTarget
        {
            // TODO: Add properties for class, method, and property info needed for codegen
        }
    }
}
