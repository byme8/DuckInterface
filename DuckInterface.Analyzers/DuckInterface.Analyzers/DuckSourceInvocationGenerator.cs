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
        private static readonly DiagnosticDescriptor DuckMappingCantBeDone =
            new DiagnosticDescriptor(
                nameof(DuckMappingCantBeDone),
                "Duck mapping is not possible",
                @"Duck mapping between '{0}' and '{1}' is not possible. Next members are missing: {2}",
                "Duck Typing",
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "Duck mapping is not possible.");

        public void Execute(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxReceiver is DuckSyntaxInvocationReceiver receiver))
            {
                return;
            }

            var duckableAttribute = context.Compilation.GetTypeByMetadataName("DuckableAttribute");
            if (context.CancellationToken.IsCancellationRequested)
            {
                return;
            }

            var ducksFromInvocations = GetDucksFromInvocations();
            var ducksFromVariableDeclarations = GetDucksFromVariableDeclarations();

            var ducks = ducksFromInvocations
                .Concat(ducksFromVariableDeclarations)
                .Distinct()
                .ToArray();

            if (context.CancellationToken.IsCancellationRequested)
            {
                return;
            }

            foreach (var (duckInterface, typeToDuck) in ducks)
            {
                var uniqueName =
                    $"{duckInterface.ToGlobalName().Replace("global::", "")}_{typeToDuck.ToGlobalName().Replace("global::", "")}";

                var properties = duckInterface
                    .GetMembers()
                    .OfType<IPropertySymbol>()
                    .SelectMany(o =>
                    {
                        var getter = o.GetMethod != null
                            ? $"             _{o.Name}Getter = () => value.{o.Name};"
                            : string.Empty;
                        var setter = o.SetMethod != null
                            ? $"             _{o.Name}Setter = o => value.{o.Name} = o;"
                            : string.Empty;

                        return new[] {getter, setter};
                    })
                    .Where(o => !string.IsNullOrEmpty(o))
                    .JoinWithNewLine();


                var methods = duckInterface
                    .GetAllMembers()
                    .GetPublicMethods()
                    .Select(o => $"             _{o.Name} = value.{o.Name};")
                    .JoinWithNewLine();


                var source = $@"
using System;

namespace {(duckInterface.ContainingNamespace.ToDisplayString())} 
{{
    public partial class D{duckInterface.Name}
    {{
        private D{duckInterface.Name}({typeToDuck.ToGlobalName()} value) 
        {{
{properties}
{methods}
        }}

        public static implicit operator D{duckInterface.Name}({typeToDuck.ToGlobalName()} value)
        {{
            return new D{duckInterface.Name}(value);
        }}
    }}
}}
";
                if (context.CancellationToken.IsCancellationRequested)
                {
                    return;
                }

                context.AddSource(uniqueName, source.ToSourceText());
            }

            IEnumerable<(ITypeSymbol DuckInteface, ITypeSymbol TypeToDuck)> GetDucksFromInvocations()
            {
                foreach (var invocation in receiver.Invocations)
                {
                    if (context.CancellationToken.IsCancellationRequested)
                    {
                        yield break;
                    }

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
                            .GetSymbolsWithName(o => o.EndsWith(nameWithoutD), SymbolFilter.Type,
                                context.CancellationToken)
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
                        var isDuckable = duckInterface.IsTypeDuckableTo(duckableSymbol);
                        if (isDuckable.IsDuckable)
                        {
                            yield return (duckInterface, duckableSymbol);
                        }
                        else
                        {
                            context.ReportDiagnostic(Diagnostic.Create(DuckMappingCantBeDone,
                                argumentSyntax.GetLocation(),
                                duckInterface.Name,
                                duckableSymbol.Name,
                                isDuckable.MissingSymbols.Select(o => o.Name).JoinWithNewLine()));
                        }
                    }
                }
            }

            IEnumerable<(ITypeSymbol DuckInteface, ITypeSymbol TypeToDuck)> GetDucksFromVariableDeclarations()
            {
                foreach (var variableDeclaration in receiver.VariableDeclarations)
                {
                    if (context.CancellationToken.IsCancellationRequested)
                    {
                        yield break;
                    }

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

                    yield return (duckInterface, typeToDuck);
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