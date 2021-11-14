using System;
using System.Collections.Generic;
using System.Linq;
using DuckInterface.Analyzers.SourceGenerators.BaseClassGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DuckInterface.Analyzers
{
    [Generator]
    public class DuckExtensionsGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new DuckExtensionsSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is not DuckExtensionsSyntaxReceiver { MemberAccesses: var members, })
            {
                return;
            }

            var duckExtensions = context.Compilation
                .GetTypeByMetadataName("DuckInterface.DuckExtensions")
                .GetAllMembers()
                .OfType<IMethodSymbol>()
                .First(o => o.Name == "Duck");

            var duckFromMethod = context.Compilation
                .GetTypeByMetadataName("DuckInterface.Duck")
                .GetAllMembers()
                .OfType<IMethodSymbol>()
                .First(o => o.Name == "From");

            var errors = new List<string>();

            try
            {
                var ducks = members
                    .Select(o =>
                    {
                        var semanticModel = context.Compilation.GetSemanticModel(o.SyntaxTree);
                        var interfaceSymbol = semanticModel.GetSymbolInfo(o.Name);

                        if (interfaceSymbol.Symbol is IMethodSymbol method)
                        {
                            if (SymbolEqualityComparer.Default.Equals(method.ReducedFrom, duckExtensions))
                            {
                                var duckInterface = method.TypeArguments.First();
                                var typeToDuck = GetTypeToDuck(o, semanticModel);

                                var (isDuckable, missingSymbols) = duckInterface.IsDuckableTo(typeToDuck);
                                if (!isDuckable)
                                {
                                    var diagnostic = Diagnostic
                                        .Create(
                                            DuckDiagnostics.DuckMappingCantBeDone,
                                            o.GetLocation(),
                                            duckInterface.Name,
                                            typeToDuck.Name,
                                            missingSymbols.Select(o => o.Name).Join());

                                    errors.Add(diagnostic.GetMessage());
                                    context.ReportDiagnostic(diagnostic);

                                    return (null, null);
                                }

                                return (Interface: duckInterface, Implementation: typeToDuck);
                            }

                            if (SymbolEqualityComparer.Default.Equals(method.ConstructedFrom, duckFromMethod))
                            {
                                var duckInterface = method.TypeArguments.First();
                                return (Interface: duckInterface, Implementation: null);
                            }
                        }

                        return (null, null);
                    });

                var processed = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
                foreach (var duck in ducks.Where(o => o.Interface is not null))
                {
                    if (context.CancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (!processed.Contains(duck.Interface))
                    {
                        BaseClassGeneratorv2.Generate(context, duck.Interface);
                        processed.Add(duck.Interface);
                    }

                    if (duck.Implementation is not null && !processed.Contains(duck.Implementation))
                    {
                        CreateDuckImplementation(context, duck.Interface, duck.Implementation);
                        CreateDuckExtensions(context, duck.Interface, duck.Implementation);
                        processed.Add(duck.Implementation);
                    }
                }

            }
            catch (Exception e)
            {
                errors.Add(e.Message);
            }
            finally
            {
                if (errors.Any())
                {
                    context.AddSource("DuckErrors", @$"
namespace DuckInterface.Generated
{{
    {Utils.EditorBrowsable}
    public class DuckErrors : DuckInterface.IDuckErrorsProvider
    {{
        public string[] Errors {{ get; }} = new string[] {{ {errors.Select(o => $"\"{o}\"").Join()} }};
    }} 
}}");
                }
            }
        }

        private void CreateDuckExtensions(GeneratorExecutionContext context, ITypeSymbol duckInterface, ITypeSymbol typeToDuck)
        {
            var duckExtensionClassName = $"Duck_{duckInterface.ToSafeGlobalName()}_{typeToDuck.ToSafeGlobalName()}_Extensions";
            var duckImplementationClassName = Utils.GetDuckImplementationClassName(duckInterface);
            var source =
                @$"using System;
using DuckInterface.Generated.{(duckInterface.ContainingNamespace.ToDisplayString())};

namespace DuckInterface
{{
    public static class {duckExtensionClassName}
    {{
        public static TInterface Duck<TInterface>(this {typeToDuck.ToGlobalName()} value) 
            where TInterface: class
        {{
            return new {duckImplementationClassName}(value) as TInterface;
        }}
    }}
}}";

            context.AddSource(duckExtensionClassName, source.ToSourceText());
        }

        private ITypeSymbol GetTypeToDuck(MemberAccessExpressionSyntax syntax, SemanticModel semanticModel)
        {
            if (syntax.Expression is ObjectCreationExpressionSyntax creation)
            {
                var type = semanticModel.GetSpeculativeTypeInfo(creation.Type.SpanStart, creation.Type, SpeculativeBindingOption.BindAsTypeOrNamespace);
                return type.Type;
            }

            var symbol = semanticModel.GetTypeInfo(syntax.Expression);
            return symbol.Type;
        }

        private void CreateDuckImplementation(GeneratorExecutionContext context, ITypeSymbol duckInterface, ITypeSymbol typeToDuck)
        {
            var fileName = $"Duck_{duckInterface.ToSafeGlobalName()}_{typeToDuck.ToSafeGlobalName()}";
            var duckClassName = Utils.GetDuckImplementationClassName(duckInterface);


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

                    return new[] { getter, setter };
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

namespace DuckInterface.Generated.{(duckInterface.ContainingNamespace.ToDisplayString())} 
{{
    public partial class {duckClassName}
    {{
        public {duckClassName}({typeToDuck.ToGlobalName()} value)
        {{
{properties}{methods}
        }}
    }}
}}
";
            if (context.CancellationToken.IsCancellationRequested)
            {
                return;
            }

            context.AddSource(fileName, source.ToSourceText());
        }
    }
}