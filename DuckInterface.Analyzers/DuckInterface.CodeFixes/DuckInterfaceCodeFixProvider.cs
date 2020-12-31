using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace DuckInterface
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DuckInterfaceCodeFixProvider)), Shared]
    public class DuckInterfaceCodeFixProvider : CodeFixProvider
    {
        public override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            throw new NotImplementedException();
        }

        public override ImmutableArray<string> FixableDiagnosticIds { get; }
    }
}
