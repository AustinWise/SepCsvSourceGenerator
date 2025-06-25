using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace SepCsvSourceGenerator;

public partial class CsvGenerator
{
    internal sealed class Emitter
    {
        public string Emit(IReadOnlyList<ParseTarget> parseTargets, CancellationToken cancellationToken)
        {
            var sb = new StringBuilder();
            foreach (var target in parseTargets)
            {
                EmitParserMethod(sb, target);
            }
            return sb.ToString();
        }

        private void EmitParserMethod(StringBuilder sb, ParseTarget target)
        {
            var method = target.MethodSymbol;
            var typeName = target.TargetType.ToDisplayString();
            var methodName = method.Name;
            var ns = target.TargetType.ContainingNamespace.ToDisplayString();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine($"    public partial class {target.TargetType.Name}");
            sb.AppendLine("    {");
            sb.AppendLine($"        public static async partial System.Collections.Generic.IAsyncEnumerable<{typeName}> {methodName}(nietras.SeparatedValues.SepReader reader, [System.Runtime.CompilerServices.EnumeratorCancellation] System.Threading.CancellationToken ct)");
            sb.AppendLine("        {");
            // Declare column indices
            foreach (var prop in target.Properties)
            {
                if (prop.HeaderName != null)
                {
                    sb.AppendLine($"            int {ToCamel(prop.Name)}Ndx;");
                }
            }
            // Required columns
            foreach (var prop in target.Properties.Where(p => p.HeaderName != null && p.Required))
            {
                sb.AppendLine($"            if (!reader.Header.TryIndexOf(\"{prop.HeaderName}\", out {ToCamel(prop.Name)}Ndx))");
                sb.AppendLine($"            {{");
                sb.AppendLine($"                throw new System.ArgumentException(\"Missing column name '{prop.HeaderName}'\");");
                sb.AppendLine($"            }}");
            }
            // Optional columns
            foreach (var prop in target.Properties.Where(p => p.HeaderName != null && !p.Required))
            {
                sb.AppendLine($"            if (!reader.Header.TryIndexOf(\"{prop.HeaderName}\", out {ToCamel(prop.Name)}Ndx))");
                sb.AppendLine($"            {{");
                sb.AppendLine($"                {ToCamel(prop.Name)}Ndx = -1;");
                sb.AppendLine($"            }}");
            }
            sb.AppendLine();
            sb.AppendLine("            await foreach (nietras.SeparatedValues.SepReader.Row row in reader)");
            sb.AppendLine("            {");
            sb.AppendLine("                ct.ThrowIfCancellationRequested();");
            sb.AppendLine($"                var ret = new {typeName}()");
            sb.AppendLine("                {");
            // Assign required properties
            foreach (var prop in target.Properties.Where(p => p.HeaderName != null && p.Required))
            {
                sb.AppendLine($"                    {prop.Name} = {GetParseExpression(prop, ToCamel(prop.Name) + "Ndx")},");
            }
            sb.AppendLine("                };");
            // Assign optional properties
            foreach (var prop in target.Properties.Where(p => p.HeaderName != null && !p.Required))
            {
                sb.AppendLine($"                if ({ToCamel(prop.Name)}Ndx != -1)");
                sb.AppendLine("                {");
                sb.AppendLine($"                    ret.{prop.Name} = {GetParseExpression(prop, ToCamel(prop.Name) + "Ndx")};");
                sb.AppendLine("                }");
            }
            sb.AppendLine("                yield return ret;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
        }

        private string ToCamel(string name)
            => char.ToLowerInvariant(name[0]) + name.Substring(1);

        private string GetParseExpression(ParseProperty prop, string ndxVar)
        {
            if (prop.Type == "string")
                return $"row[{ndxVar}].Span.ToString()";
            if (prop.Type == "DateTime" && !string.IsNullOrEmpty(prop.DateFormat))
                return $"System.DateTime.ParseExact(row[{ndxVar}].Span, \"{prop.DateFormat}\", System.Globalization.CultureInfo.InvariantCulture)";
            if (prop.Type == "DateTime")
                return $"System.DateTime.Parse(row[{ndxVar}].Span, System.Globalization.CultureInfo.InvariantCulture)";
            if (prop.Type.StartsWith("System.Nullable<") || prop.IsNullable)
                return $"{GetNonNullableType(prop.Type)}.Parse(row[{ndxVar}].Span, System.Globalization.CultureInfo.InvariantCulture)";
            // Default: ISpanParsable<T>
            return $"{prop.Type}.Parse(row[{ndxVar}].Span, System.Globalization.CultureInfo.InvariantCulture)";
        }

        private string GetNonNullableType(string type)
        {
            if (type.StartsWith("System.Nullable<"))
                return type.Substring("System.Nullable<".Length, type.Length - "System.Nullable<".Length - 1);
            if (type.EndsWith("?"))
                return type.TrimEnd('?');
            return type;
        }
    }
}
