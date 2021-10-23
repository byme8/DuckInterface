using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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

            var errors = new List<string>();
            foreach (var member in members)
            {
                if (context.CancellationToken.IsCancellationRequested)
                {
                    return;
                }
                
                var semanticModel = context.Compilation.GetSemanticModel(member.SyntaxTree);
                var interfaceSymbol = semanticModel.GetSymbolInfo(member.Name);

                if (interfaceSymbol.Symbol is not IMethodSymbol method ||
                    !SymbolEqualityComparer.Default.Equals(method.ReducedFrom, duckExtensions))
                {
                    continue;
                }

                var duckInterface = method.TypeArguments.First();
                var typeToDuck = GetTypeToDuck(member, semanticModel);

                var (isDuckable, missingSymbols) = duckInterface.IsDuckableTo(typeToDuck);
                if (!isDuckable)
                {
                    var diagnostic = Diagnostic
                        .Create(
                            DuckDiagnostics.DuckMappingCantBeDone,
                            member.GetLocation(),
                            duckInterface.Name,
                            typeToDuck.Name,
                            missingSymbols.Select(o => o.Name).Join());

                    errors.Add(diagnostic.GetMessage());
                    context.ReportDiagnostic(diagnostic);

                    continue;
                }

                CreateDuckClass(context, duckInterface);
                CreateDuckImplementation(context, duckInterface, typeToDuck);
                CreateDuckExtensions(context, duckInterface, typeToDuck);
            }

            if (errors.Any())
            {
                context.AddSource("DuckErrors", errors.Select(o => "// " + o).JoinWithNewLine());
            }
        }

        private void CreateDuckExtensions(GeneratorExecutionContext context, ITypeSymbol duckInterface, ITypeSymbol typeToDuck)
        {
            var duckExtensionClassName = $"Duck_{duckInterface.ToSafeGlobalName()}_{typeToDuck.ToSafeGlobalName()}_Extensions";
            var duckImplementationClassName = GetDuckImplementationClassName(duckInterface);
            var source =
                @$"using System;

namespace DuckInterface.Generated.{(duckInterface.ContainingNamespace.ToDisplayString())} 
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
            var duckClassName = GetDuckImplementationClassName(duckInterface);


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

        private static string GetDuckImplementationClassName(ITypeSymbol duckInterface)
        {
            return $"Duck_{duckInterface.ToSafeGlobalName()}";
        }

        private void CreateDuckClass(GeneratorExecutionContext context, ITypeSymbol duckInterface)
        {
            var duckImplementationClassName = GetDuckImplementationClassName(duckInterface);
            var fields = CreateSourceForFields(context, duckInterface);

            var fullMethods = duckInterface
                .GetAllMembers()
                .GetPublicMethods()
                .Select(method =>
                {
                    var returnType = method.ReturnType;
                    if (returnType.IsRefLikeType)
                    {
                        return "";
                    }

                    var parameters = method.Parameters;
                    if (parameters.Any(o => o.Type.IsRefLikeType))
                    {
                        return "";
                    }

                    return
                        $@"
        [System.Diagnostics.DebuggerStepThrough]
        public {returnType.ToGlobalName()} {method.Name}({parameters.Select(o => $"{o.Type.ToGlobalName()} {o.Name}").Join()})
        {{
            {(returnType.SpecialType == SpecialType.System_Void ? "" : "return ")}_{method.Name}({parameters.Select(o => o.Name).Join()});
        }}
";
                });

            var properties = duckInterface
                .GetAllMembers()
                .OfType<IPropertySymbol>()
                .Select(property => $@"
        public {property.Type.ToGlobalName()} {property.Name}
        {{
            {(property.GetMethod != null ? $" [System.Diagnostics.DebuggerStepThrough] get {{ return _{property.Name}Getter(); }}" : string.Empty)}
            {(property.SetMethod != null ? $" [System.Diagnostics.DebuggerStepThrough] set {{ _{property.Name}Setter(value); }}" : string.Empty)}
        }}
");

            var source = $@"
using System;

namespace DuckInterface.Generated.{duckInterface.ContainingNamespace.ToDisplayString(new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces))} 
{{
    {Utils.EditorBrowsable}
    public partial class {duckImplementationClassName}: {duckInterface.ToGlobalName()} 
    {{
{fields.JoinWithNewLine()}        
{properties.JoinWithNewLine()}
{fullMethods.JoinWithNewLine()}        
    }}
}}
";
            context.AddSource(duckImplementationClassName, source.ToSourceText());
        }

        private IEnumerable<string> CreateSourceForFields(
            GeneratorExecutionContext context,
            ITypeSymbol duckedType)
        {
            var methods = duckedType
                .GetAllMembers()
                .GetPublicMethods()
                .Select(method =>
                {
                    var returnType = method.ReturnType;
                    var arguments = method.Parameters;

                    (string FuncOrAction, string ReturnType) @return =
                        method.ReturnsVoid
                            ? ("Action", string.Empty)
                            : ("Func", returnType.ToGlobalName());

                    (string Left, string Right) wrappers =
                        arguments.Any() || @return.FuncOrAction == "Func"
                            ? ("<", ">")
                            : ("", "");

                    return $@"
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)] 
        private readonly {@return.FuncOrAction}{arguments
            .Select(o => o.Type.ToGlobalName())
            .Concat(new[] { @return.ReturnType })
            .Where(o => !string.IsNullOrEmpty(o))
            .Join()
            .Wrap(wrappers.Left, wrappers.Right)} _{method.Name};";
                });

            var properties = duckedType
                .GetAllMembers()
                .OfType<IPropertySymbol>()
                .Where(o => o.DeclaredAccessibility == Accessibility.Public)
                .SelectMany(property =>
                {
                    var propertyType = property.Type.ToGlobalName();
                    var getter = property.GetMethod != null
                        ? $@"
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)] 
        private readonly Func<{propertyType}> _{property.Name}Getter;"
                        : string.Empty;

                    var setter = property.SetMethod != null
                        ? $@"
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)] 
        private readonly Action<{propertyType}> _{property.Name}Setter;"
                        : string.Empty;

                    return new[] { getter, setter };
                });
            return properties.Concat(methods);
        }
    }
}