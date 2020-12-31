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
            var callsWithDuckInterface = receiver.Invocations
                .Select(o =>
                {
                    var semanticModel = context.Compilation.GetSemanticModel(o.SyntaxTree);
                    var symbol =
                        semanticModel.GetSpeculativeSymbolInfo(o.SpanStart, o,
                            SpeculativeBindingOption.BindAsExpression);

                    return (Symbol: symbol.CandidateSymbols.FirstOrDefault() as IMethodSymbol, SyntaxNode: o);
                })
                .Where(o => o.Symbol != null)
                .Where(o => o.Symbol.Parameters
                    .Any(parameter => parameter.Type.GetAttributes()
                        .Any(attribute => attribute.AttributeClass.Equals(duckAttribute))));

            foreach (var call in callsWithDuckInterface)
            {
                var duckableParamenters = call.Symbol.Parameters
                    .Select((o, i) => (
                        Index: i,
                        DuckInteface: o.Type,
                        IsDuckInteface: o.Type
                            .GetAttributes()
                            .Any(attr => attr.AttributeClass.Equals(duckAttribute))))
                    .Where(o => o.IsDuckInteface);

                foreach (var duckableParamenter in duckableParamenters)
                {
                    var argument = call
                        .SyntaxNode
                        .ArgumentList
                        .Arguments[duckableParamenter.Index]
                        .Expression;
                    
                    var semanticModel = context.Compilation.GetSemanticModel(argument.SyntaxTree);
                    var duckableSymbol = semanticModel
                        .GetSpeculativeSymbolInfo(argument.SpanStart, argument,
                            SpeculativeBindingOption.BindAsExpression).Symbol as ILocalSymbol;

                    var canBeDuck = duckableParamenter.DuckInteface
                        .IsTypeDuckableTo(duckableSymbol.Type);

                    if (!canBeDuck)
                    {
                        continue;
                    }

                    var uniqueName = duckableSymbol.Type
                        .GetUniqueName();
                    
                    var source = $@"
using System;

namespace {(duckableParamenter.DuckInteface.ContainingNamespace.ToDisplayString())} 
{{
    public partial struct {duckableParamenter.DuckInteface.Name}
    {{
        private {duckableParamenter.DuckInteface.Name}({duckableSymbol.Type.ToGlobalName()} value) 
        {{
{duckableParamenter
                        .DuckInteface
                        .GetMembers()
                        .OfType<IMethodSymbol>()
                        .Where(o => o.Name != ".ctor")
                        .Select(o => $"             _{o.Name} = value.{o.Name};")
                        .JoinWithNewLine()}
        }}

        public static implicit operator {duckableParamenter.DuckInteface.Name}({duckableSymbol.Type.ToGlobalName()} value)
        {{
            return new {duckableParamenter.DuckInteface.Name}(value);
        }}
    }}
}}
";
                    context.AddSource(uniqueName, source.ToSourceText());
                }
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

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is InvocationExpressionSyntax invocationExpressionSyntax)
            {
                Invocations.Add(invocationExpressionSyntax);
            }
        }
    }
}