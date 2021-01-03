using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DuckInterface
{
    [Generator]
    public class DuckSourceInterfaceGenerator : ISourceGenerator
    {
        private static readonly DiagnosticDescriptor DuckCantHandleRefStructs =
            new DiagnosticDescriptor(
                nameof(DuckCantHandleRefStructs),
                "DuckInterface can't handle the ref structs",
                "DuckInterface can't handle ref structs",
                "Duck Typing",
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "Duck mapping is not possible.");

        public void Execute(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxReceiver is DuckSyntaxInterfaceReceiver receiver))
            {
                return;
            }

            var duckableTypes = receiver.DuckableTypes;
            foreach (var duckedTypeDeclaration in duckableTypes)
            {
                var semanticModel = context.Compilation.GetSemanticModel(duckedTypeDeclaration.SyntaxTree);
                var duckedType = semanticModel.GetDeclaredSymbol(duckedTypeDeclaration, context.CancellationToken);
                var uniqueName = $"D{duckedType.Name}.cs";

                var fields = CreateSourceForFields(context, duckedType);
                var fullMethods = duckedType
                    .GetAllMembers()
                    .GetPublicMethods()
                    .Select(method =>
                    {
                        var returnType = method.ReturnType;
                        if (returnType.IsRefLikeType)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(DuckCantHandleRefStructs,
                                duckedTypeDeclaration.GetLocation(), DiagnosticSeverity.Error));

                            return "";
                        }

                        var parameters = method.Parameters;
                        if (parameters.Any(o => o.Type.IsRefLikeType))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(DuckCantHandleRefStructs,
                                duckedTypeDeclaration.GetLocation(), DiagnosticSeverity.Error));

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

                var properties = duckedType
                    .GetAllMembers()
                    .OfType<IPropertySymbol>()
                    .Select(property =>
                    {
                        var returnType = property.Type;
                        if (returnType.IsRefLikeType)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(DuckCantHandleRefStructs,
                                duckedTypeDeclaration.GetLocation(), DiagnosticSeverity.Error));

                            return "";
                        }

                        return
                            $@"
        public {returnType.ToGlobalName()} {property.Name}
        {{
            {(property.GetMethod != null ? $" [System.Diagnostics.DebuggerStepThrough] get {{ return _{property.Name}Getter(); }}" : string.Empty)}
            {(property.SetMethod != null ? $" [System.Diagnostics.DebuggerStepThrough] set {{ _{property.Name}Setter(value); }}" : string.Empty)}
        }}
";
                    });


                var source = $@"
using System;

namespace {duckedType.ContainingNamespace.ToDisplayString(new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces))} 
{{
    public partial class D{duckedType.Name} : {duckedType.Name} 
    {{
{fields.JoinWithNewLine()}        
{properties.JoinWithNewLine()}
{fullMethods.JoinWithNewLine()}        
    }}
}}
";
                context.AddSource(uniqueName, source.ToSourceText());
            }
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
                        .Concat(new[] {@return.ReturnType})
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

                    return new[] {getter, setter};
                });

            return properties.Concat(methods);
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