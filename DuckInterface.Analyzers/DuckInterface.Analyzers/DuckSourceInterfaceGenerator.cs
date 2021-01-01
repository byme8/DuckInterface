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
        public void Execute(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxReceiver is DuckSyntaxInterfaceReceiver receiver))
            {
                return;
            }

            var duckableTypes = receiver.DuckableTypes;


            foreach (var duckedType in duckableTypes)
            {
                var uniqueName = $"D{duckedType.Identifier.Text}.cs";
                var fields = CreateSourceForFields(context, duckedType);

                var fullMethods = duckedType
                    .Members
                    .OfType<MethodDeclarationSyntax>()
                    .Select(method =>
                    {
                        var semanticModel = context.Compilation.GetSemanticModel(method.SyntaxTree);
                        var returnType = semanticModel
                            .GetSpeculativeSymbolInfo(method.ReturnType.SpanStart, method.ReturnType,
                                SpeculativeBindingOption.BindAsTypeOrNamespace)
                            .GetTypeSymbol();

                        var parameters = method
                            .DescendantNodes()
                            .OfType<ParameterSyntax>()
                            .ToArray();

                        return
                            $@"
        [System.Diagnostics.DebuggerStepThrough]
        public {returnType.ToGlobalName()} {method.Identifier.Text}({parameters.Select(o => $"{o.Type.ToString()} {o.Identifier.Text}").Join()})
        {{
            {(returnType.SpecialType == SpecialType.System_Void ? "" : "return ")}_{method.Identifier.Text}({parameters.Select(o => o.Identifier.Text).Join()});
        }}
";
                    });

                var properties = duckedType
                    .Members
                    .OfType<PropertyDeclarationSyntax>()
                    .Select(property =>
                    {
                        var semanticModel = context.Compilation.GetSemanticModel(property.SyntaxTree);
                        var returnType = semanticModel.GetSpeculativeSymbolInfo(property.Type.SpanStart, property.Type,
                            SpeculativeBindingOption.BindAsTypeOrNamespace).GetTypeSymbol();

                        return
                            $@"
        public {returnType.ToGlobalName()} {property.Identifier.Text}
        {{
            {(property.AccessorList.Accessors.Any(o => o.Keyword.Text == "get") ? $" [System.Diagnostics.DebuggerStepThrough] get {{ return _{property.Identifier.Text}Getter(); }}" : string.Empty)}
            {(property.AccessorList.Accessors.Any(o => o.Keyword.Text == "set") ? $" [System.Diagnostics.DebuggerStepThrough] set {{ _{property.Identifier.Text}Setter(value); }}" : string.Empty)}
        }}
";
                    });


                var source = $@"
using System;

namespace {(duckedType.Parent as NamespaceDeclarationSyntax).Name} 
{{
    public partial class D{duckedType.Identifier.Text} : {duckedType.Identifier.Text} 
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
            TypeDeclarationSyntax duckedType)
        {
            var methods = duckedType
                .Members
                .OfType<MethodDeclarationSyntax>()
                .Select(method =>
                {
                    var semanticModel = context.Compilation.GetSemanticModel(method.ReturnType.SyntaxTree);
                    var returnType = semanticModel.GetSpeculativeSymbolInfo(method.ReturnType.SpanStart,
                        method.ReturnType, SpeculativeBindingOption.BindAsTypeOrNamespace).GetTypeSymbol();

                    var arguments = method.ParameterList.Parameters
                        .Select(o => semanticModel.GetSpeculativeSymbolInfo(o.Type.SpanStart, o.Type,
                                SpeculativeBindingOption.BindAsTypeOrNamespace)
                            .GetTypeSymbol())
                        .ToArray();

                    (string FuncOrAction, string ReturnType) @return =
                        returnType.SpecialType == SpecialType.System_Void
                            ? ("Action", "")
                            : ("Func", returnType.ToGlobalName());
                    
                    (string Left, string Right) wrappers = arguments.Any() 
                        ? ("<", ">") 
                        : ("", "");

                    return $@"
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)] 
        private readonly {@return.FuncOrAction}{arguments
                        .Select(o => o.ToGlobalName())
                        .Concat(new[] {@return.ReturnType})
                        .Join()
                        .Wrap(wrappers.Left, wrappers.Right)} _{method.Identifier.Text};";
                });

            var properties = duckedType
                .Members
                .OfType<PropertyDeclarationSyntax>()
                .SelectMany(property =>
                {
                    var semanticModel = context.Compilation.GetSemanticModel(property.SyntaxTree);
                    var propertyTypeInfo = semanticModel.GetSpeculativeSymbolInfo(property.Type.SpanStart,
                        property.Type, SpeculativeBindingOption.BindAsTypeOrNamespace);

                    if (propertyTypeInfo.Symbol is null)
                    {
                        return new string[0];
                    }

                    var propertyType = propertyTypeInfo.GetTypeSymbol().ToGlobalName();
                    var getter = property.AccessorList.Accessors.Any(o => o.Keyword.Text == "get")
                        ? $@"
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)] 
        private readonly Func<{propertyType}> _{property.Identifier.Text}Getter;"
                        : string.Empty;

                    var setter = property.AccessorList.Accessors.Any(o => o.Keyword.Text == "set")
                        ? $@"
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)] 
        private readonly Action<{propertyType}> _{property.Identifier.Text}Setter;"
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