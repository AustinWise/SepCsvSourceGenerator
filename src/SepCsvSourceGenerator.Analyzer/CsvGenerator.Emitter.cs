using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace SepCsvSourceGenerator;

public partial class CsvGenerator
{
    internal sealed class Emitter
    {
        public string Emit(IReadOnlyList<Parser.CsvParseTarget> parseTargets, CancellationToken cancellationToken)
        {
            var sb = new StringBuilder();
            // TODO: Generate code for each parse target
            return sb.ToString();
        }
    }
}
