using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DuckInterface
{
    [Generator]
    public class DuckSourceInterfaceGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxReceiver is DuckSyntaxInterfaceReceiver receiver))
            {
                return;
            }

            var duckableTypes = receiver.DuckableTypes;

            foreach (var duckedType in duckableTypes)
            {
                var uniqueName = $"{duckedType.Identifier.Text}_{Guid.NewGuid().ToString().Replace("-", "")}";
                var fields = duckedType
                    .Members
                    .OfType<MethodDeclarationSyntax>()
                    .Select(method =>
                    {
                        var types = method
                            .DescendantNodes()
                            .OfType<PredefinedTypeSyntax>()
                            .Select(oo => oo.Keyword.Text)
                            .ToArray();

                        return
                            $"private readonly Func<{types.Skip(1).Concat(types.Take(1)).Join()}> _{method.Identifier.Text};";
                    });
                
                var fullMethods = duckedType
                    .Members
                    .OfType<MethodDeclarationSyntax>()
                    .Select(method =>
                    {
                        var returnType = method
                            .DescendantNodes()
                            .OfType<PredefinedTypeSyntax>()
                            .Select(oo => oo.Keyword.Text)
                            .First();

                        var parameters = method
                            .DescendantNodes()
                            .OfType<ParameterSyntax>()
                            .ToArray();
                        
                        return
$@"
    public {method.Modifiers.Select(o => o.Text).Join(" ")} {returnType} {method.Identifier.Text}({parameters.Select(o => $"{o.Type.ToString()} {o.Identifier.Text}").Join()})
    {{
        return _{method.Identifier.Text}({parameters.Select(o => o.Identifier.Text).Join()});
    }}
";
                    });
                
                
                var source = $@"
using System;

namespace {(duckedType.Parent as NamespaceDeclarationSyntax).Name} 
{{
    public partial class D{duckedType.Identifier.Text}
    {{
        {fields.JoinWithNewLine()}        
        {fullMethods.JoinWithNewLine()}        
    }}
}}
";
                context.AddSource(uniqueName, source.ToSourceText());
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new DuckSyntaxInterfaceReceiver());
        }
    }
    

    public class DuckSyntaxInterfaceReceiver : ISyntaxReceiver
    {
        public List<TypeDeclarationSyntax> DuckableTypes { get; }
            = new List<TypeDeclarationSyntax>();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is TypeDeclarationSyntax structDeclarationSyntax &&
                structDeclarationSyntax.AttributeLists
                    .SelectMany(o => o
                        .DescendantNodes()
                        .OfType<IdentifierNameSyntax>())
                    .Any(o => o.Identifier.Text.EndsWith("Duckable")))
            {
                DuckableTypes.Add(structDeclarationSyntax);
            }
        }
    }
}