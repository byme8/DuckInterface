using Microsoft.CodeAnalysis;

namespace DuckInterface.Analyzers
{
    public static class DuckDiagnostics
    {
        public static readonly DiagnosticDescriptor DuckMappingCantBeDone =
            new DiagnosticDescriptor(
                nameof(DuckMappingCantBeDone),
                "Duck mapping is not possible",
                @"Duck mapping between '{0}' and '{1}' is not possible. Next members are missing: {2}",
                "Duck Typing",
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "Duck mapping is not possible.");
    }
}