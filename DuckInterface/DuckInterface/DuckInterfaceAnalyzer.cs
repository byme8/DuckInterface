using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DuckInterface
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DuckInterfaceAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DuckInterface";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Ducking";

        private static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(
                DiagnosticId,
                Title,
                MessageFormat,
                Category,
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true,
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(Rule);
            }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(Handle, SyntaxKind.InvocationExpression);
        }

        private void Handle(SyntaxNodeAnalysisContext obj)
        {
            
        }
    }
}
