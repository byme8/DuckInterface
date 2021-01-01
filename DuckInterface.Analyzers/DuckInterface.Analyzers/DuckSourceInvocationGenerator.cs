using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DuckInterface
{
    [Generator]
    public class DuckSourceInvocationGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxReceiver is DuckSyntaxInvocationReceiver receiver))
            {
                return;
            }

            var duckAttribute = context.Compilation.GetTypeByMetadataName("DuckAttribute");
            var callsArgumentsWithDuckInterface = receiver.Invocations
                .SelectMany(invocation =>
                {
                    var semanticModel = context.Compilation.GetSemanticModel(invocation.SyntaxTree);
                    var symbol =
                        semanticModel.GetSpeculativeSymbolInfo(invocation.SpanStart, invocation,
                            SpeculativeBindingOption.BindAsExpression);

                    var call = symbol.CandidateSymbols.FirstOrDefault() as IMethodSymbol;
                    if (call is null)
                    {
                        return new (ITypeSymbol, ITypeSymbol)[0];
                    }

                    return call
                        .Parameters
                        .Select((o, i) => (
                            Index: i,
                            DuckInteface: o.Type,
                            IsDuckInteface: o.Type
                                .GetAttributes()
                                .Any(attr => attr.AttributeClass.Equals(duckAttribute))))
                        .Where(o => o.IsDuckInteface)
                        .Select(o =>
                        {
                            TypeSyntax argumentSyntax = null;
                            switch (invocation.ArgumentList
                                    .Arguments[o.Index]
                                    .Expression)
                            {
                                case IdentifierNameSyntax name:
                                    argumentSyntax = name;
                                    break;
                                case ObjectCreationExpressionSyntax ctor:
                                    argumentSyntax = ctor.Type;
                                    break;
                            }

                            if (argumentSyntax is null)
                            {
                                return (null, null);
                            }

                            var speculativeArgumentSymbol = semanticModel
                                .GetSpeculativeSymbolInfo(argumentSyntax.SpanStart, argumentSyntax,
                                    SpeculativeBindingOption.BindAsExpression);

                            var duckableSymbol = speculativeArgumentSymbol.GetTypeSymbol();

                            return (o.DuckInteface, TypeToDuck: duckableSymbol);
                        });
                })
                .ToArray();

            var variableDeclarationsWithDuckInterface = receiver.VariableDeclarations
                .Select(o =>
                {
                    var semanticModel = context.Compilation.GetSemanticModel(o.SyntaxTree);
                    var duckInterfaceSymbol =
                        semanticModel.GetSpeculativeSymbolInfo(o.Type.SpanStart, o.Type,
                            SpeculativeBindingOption.BindAsExpression);

                    if (duckInterfaceSymbol.Symbol == null ||
                        !duckInterfaceSymbol.Symbol
                            .GetAttributes()
                            .Any(attribute => attribute.AttributeClass.Equals(duckAttribute)))
                    {
                        return (null, null);
                    }

                    var value = o.DescendantNodes().OfType<EqualsValueClauseSyntax>().First().Value;
                    var typeToDuckSymbol =
                        semanticModel.GetSpeculativeSymbolInfo(value.SpanStart, value,
                            SpeculativeBindingOption.BindAsExpression);

                    return (DuckInteface: duckInterfaceSymbol.GetTypeSymbol(), TypeToDuck: typeToDuckSymbol.GetTypeSymbol());
                })
                .ToArray();

            var ducks = callsArgumentsWithDuckInterface
                .Concat(variableDeclarationsWithDuckInterface)
                .Distinct()
                .Where(o => 
                    o.DuckInteface != null && 
                    o.TypeToDuck != null && 
                    o.DuckInteface.IsTypeDuckableTo(o.TypeToDuck))
                .ToArray();

            foreach (var (duckInterface, typeToDuck) in ducks)
            {
                var uniqueName = duckInterface
                    .GetUniqueName();

                var source = $@"
using System;

namespace {(duckInterface.ContainingNamespace.ToDisplayString())} 
{{
    public partial {(duckInterface.TypeKind == TypeKind.Struct ? "struct" : "class")} {duckInterface.Name}
    {{
        private {duckInterface.Name}({duckInterface.ToGlobalName()} value) 
        {{
{duckInterface
                    .GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(o => o.Name != ".ctor")
                    .Select(o => $"             _{o.Name} = value.{o.Name};")
                    .JoinWithNewLine()}
        }}

        public static implicit operator {duckInterface.Name}({typeToDuck.ToGlobalName()} value)
        {{
            return new {duckInterface.Name}(value);
        }}
    }}
}}
";
                context.AddSource(uniqueName, source.ToSourceText());
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new DuckSyntaxInvocationReceiver());
        }
    }

    public class DuckSyntaxInvocationReceiver : ISyntaxReceiver
    {
        public List<InvocationExpressionSyntax> Invocations { get; }
            = new List<InvocationExpressionSyntax>();

        public List<VariableDeclarationSyntax> VariableDeclarations { get; }
            = new List<VariableDeclarationSyntax>();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            switch (syntaxNode)
            {
                case InvocationExpressionSyntax invocation:
                    Invocations.Add(invocation);
                    break;
                case VariableDeclarationSyntax variableDeclarationSyntax:
                    VariableDeclarations.Add(variableDeclarationSyntax);
                    break;
            }
        }
    }
}