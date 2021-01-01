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
            var duckableAttribute = context.Compilation.GetTypeByMetadataName("DuckableAttribute");
            
            var ducksFromInvocations = GetDucksFromInvocations();
            var ducksFromVariableDeclarations = GetDucksFromVariableDeclarations();

            var ducks = ducksFromInvocations
                .Concat(ducksFromVariableDeclarations)
                .Distinct()
                .Where(o => o.DuckInteface.IsTypeDuckableTo(o.TypeToDuck))
                .ToArray();

            foreach (var (duckInterface, typeToDuck) in ducks)
            {
                var uniqueName = duckInterface
                    .GetUniqueName();

                var source = $@"
using System;

namespace {(duckInterface.ContainingNamespace.ToDisplayString())} 
{{
    public partial class D{duckInterface.Name}
    {{
        private D{duckInterface.Name}({typeToDuck.ToGlobalName()} value) 
        {{
{duckInterface
        .GetMembers()
        .OfType<IMethodSymbol>()
        .Where(o => o.Name != ".ctor")
        .Select(o => $"             _{o.Name} = value.{o.Name};")
        .JoinWithNewLine()}
        }}

        public static implicit operator D{duckInterface.Name}({typeToDuck.ToGlobalName()} value)
        {{
            return new D{duckInterface.Name}(value);
        }}
    }}
}}
";
                context.AddSource(uniqueName, source.ToSourceText());
            }

            IEnumerable<(ITypeSymbol DuckInteface, ITypeSymbol TypeToDuck)> GetDucksFromInvocations()
            {
                foreach (var invocation in receiver.Invocations)
                {
                    var semanticModel = context.Compilation.GetSemanticModel(invocation.SyntaxTree);
                    var symbol =
                        semanticModel.GetSpeculativeSymbolInfo(invocation.SpanStart, invocation,
                            SpeculativeBindingOption.BindAsExpression);

                    var call = symbol.CandidateSymbols.FirstOrDefault() as IMethodSymbol;
                    if (call is null)
                    {
                        continue;
                    }

                    for (int i = 0; i < call.Parameters.Length; i++)
                    {
                        var parameter = call.Parameters[i];
                        if (!parameter.Type.Name.StartsWith("DI"))
                        {
                            continue;
                        }
                        
                        var nameWithoutD = parameter.Type.Name.Substring(1);
                        var duckInterface = context.Compilation
                            .GetSymbolsWithName(o => o.EndsWith(nameWithoutD), SymbolFilter.Type, context.CancellationToken)
                            .OfType<ITypeSymbol>()
                            .FirstOrDefault(s => s
                                .GetAttributes()
                                .Any(attr => attr.AttributeClass.Equals(duckableAttribute)));

                        if (duckInterface is null)
                        {
                            continue;
                        }
                        
                        TypeSyntax argumentSyntax = null;
                        switch (invocation.ArgumentList.Arguments[i].Expression)
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
                            continue;
                        }

                        var speculativeArgumentSymbol = semanticModel
                            .GetSpeculativeSymbolInfo(argumentSyntax.SpanStart, argumentSyntax,
                                SpeculativeBindingOption.BindAsExpression);

                        var duckableSymbol = speculativeArgumentSymbol.GetTypeSymbol();

                        yield return (duckInterface, duckableSymbol);
                    }
                }
            }

            IEnumerable<(ITypeSymbol DuckInteface, ITypeSymbol TypeToDuck)> GetDucksFromVariableDeclarations()
            {
                foreach (var variableDeclaration in receiver.VariableDeclarations)
                {
                    var semanticModel = context.Compilation.GetSemanticModel(variableDeclaration.SyntaxTree);
                    var nameWithoutD = variableDeclaration.Type.ToString().Substring(1);
                    var duckInterface = context.Compilation
                        .GetSymbolsWithName(o => o.EndsWith(nameWithoutD), SymbolFilter.Type, context.CancellationToken)
                        .OfType<ITypeSymbol>()
                        .FirstOrDefault(s => s
                            .GetAttributes()
                            .Any(attr => attr.AttributeClass.Equals(duckableAttribute)));

                    if (duckInterface is null)
                    {
                        continue;
                    }
                    
                    var value = variableDeclaration.DescendantNodes().OfType<EqualsValueClauseSyntax>().First().Value;
                    var typeToDuckSymbol =
                        semanticModel.GetSpeculativeSymbolInfo(value.SpanStart, value,
                            SpeculativeBindingOption.BindAsExpression);

                    var typeToDuck = typeToDuckSymbol.GetTypeSymbol();
                    if (typeToDuck is null)
                    {
                        continue;
                    }
                    
                    yield return  (duckInterface, typeToDuck);
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