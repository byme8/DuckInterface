using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DuckInterface
{
    [Generator]
    public class DuckSourceBaseGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            context.AddSource("DuckAttribute",
                SourceText.From(@"
    using System;

    public class DuckAttribute : Attribute { }
", Encoding.UTF8));
        }

        public void Initialize(GeneratorInitializationContext context)
        {
        }
    }
}