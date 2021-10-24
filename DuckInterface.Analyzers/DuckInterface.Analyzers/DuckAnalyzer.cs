using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DuckInterface.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DuckAnalyzer : DiagnosticAnalyzer
    {
        public override void Initialize(AnalysisContext context)
        {

        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
    }
}