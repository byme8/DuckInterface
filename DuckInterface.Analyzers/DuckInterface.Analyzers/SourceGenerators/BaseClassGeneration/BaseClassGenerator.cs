using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace DuckInterface.Analyzers.SourceGenerators.BaseClassGeneration
{
    public static class BaseClassGenerator
    {
        public static void Generate(GeneratorExecutionContext context, ITypeSymbol duckInterface)
        {
            var containingNamespace = duckInterface.ContainingNamespace.ToDisplayString(new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces));
            var @namespace = new StringBuilder();
            @namespace.Append("DuckInterface.Generated");
            if (!string.IsNullOrEmpty(containingNamespace))
            {
                @namespace.Append(".");
                @namespace.Append(containingNamespace);
            }
            
            var duckInterfaceGlobalName = duckInterface.ToGlobalName();

            var duckImplementationClassName = Utils.GetDuckImplementationClassName(duckInterface);
            var arguments = CreateConstructorArguments(duckInterface).ToArray();
            var formatted = arguments
                .SelectMany(FormatMakeArguments)
                .ToArray();

            var mergedArguments = formatted
                .Select(o => $"{o.Type} {o.Name}")
                .Join();

            var optionalArguments = formatted
                .Select(o => $"{o.Type} {o.Name} = default")
                .Join();

            var createExtensions = CreateExtensions();
            var constructorBody = CreateConstructorBody(duckInterface);
            var fields = CreateSourceForFields(duckInterface);
            var fullMethods = CreateSourceForMethods(duckInterface);
            var properties = CreateSourceForProperties(duckInterface);

            var source = $@"using System;
using DuckInterface.Core;

namespace DuckInterface
{{
    public static class DuckHandlerExtensionsFor{duckInterface.ToSafeGlobalName()}
    {{
{createExtensions}

        public static {duckInterfaceGlobalName} Make(this IDuckHandler<{duckInterfaceGlobalName}> handler, {mergedArguments})
        {{
            return new {@namespace}.{duckImplementationClassName}({formatted.Select(o => o.Name).Join()});
        }}

        public static {duckInterfaceGlobalName} MakePartial(this IDuckHandler<{duckInterfaceGlobalName}> handler, {optionalArguments})
        {{
            return new {@namespace}.{duckImplementationClassName}({formatted.Select(o => o.Name).Join()});
        }}
    }}
}}

namespace {@namespace} 
{{
    {Utils.EditorBrowsable}
    public partial class {duckImplementationClassName}: {duckInterfaceGlobalName} 
    {{
        public {duckImplementationClassName}({mergedArguments})
        {{
            {constructorBody.JoinWithNewLine(";")};
        }}
{fields.JoinWithNewLine()}        
{properties.JoinWithNewLine()}
{fullMethods.JoinWithNewLine()}        
    }}
}}
";
            context.AddSource(duckImplementationClassName, source.ToSourceText());

            IEnumerable<(string Type, string Name)> FormatCreateArguments(ISymbol symbol)
            {
                switch (symbol)
                {
                    case IPropertySymbol property:
                    {
                        var name = property.Name.LowerFirstChar();
                        yield return (property.Type.ToGlobalName(), name);

                        break;
                    }
                    case IMethodSymbol { MethodKind: MethodKind.Ordinary } method:
                    {
                        var name = method.Name.LowerFirstChar();
                        if (method.ReturnsVoid)
                        {
                            yield return ($"Action<{method.Parameters.Select(o => o.Type.ToGlobalName()).Join()}>", $"{name}");
                        }
                        else
                        {
                            var types = new[] { method.ReturnType.ToGlobalName() }.Concat(method.Parameters.Select(o => o.Type.ToGlobalName()));
                            yield return ($"Func<{types.Join()}>", $"{name}");
                        }

                        break;
                    }
                }
            }

            IEnumerable<string> FormatCreateConstructorArguments(ISymbol symbol)
            {
                switch (symbol)
                {
                    case IPropertySymbol property:
                    {
                        var name = property.Name.LowerFirstChar();
                        if (property.GetMethod is not null)
                        {
                            yield return $"() => {name}";
                        }

                        if (property.SetMethod is not null)
                        {
                            yield return $"o => {name} = o";
                        }

                        break;
                    }
                    case IMethodSymbol { MethodKind: MethodKind.Ordinary } method:
                    {
                        var name = method.Name.LowerFirstChar();
                        yield return name;
                        break;
                    }
                }
            }

            string CreateExtensions()
            {
                var createMergeArguments = arguments
                    .SelectMany(FormatCreateArguments)
                    .Select(o => $"{o.Type} {o.Name}")
                    .Join();

                var optionalCreateMergeArguments = arguments
                    .SelectMany(FormatCreateArguments)
                    .Select(o => $"{o.Type} {o.Name} = default")
                    .Join();

                var createMergedConstructorArguments = arguments
                    .SelectMany(FormatCreateConstructorArguments)
                    .Join();

                return
                    @$"
        public static {duckInterfaceGlobalName} Create(this IDuckHandler<{duckInterfaceGlobalName}> handler, {createMergeArguments})
        {{
            return new {@namespace}.{duckImplementationClassName}({createMergedConstructorArguments});
        }}

        public static {duckInterfaceGlobalName} CreatePartial(this IDuckHandler<{duckInterfaceGlobalName}> handler, {optionalCreateMergeArguments})
        {{
            return new {@namespace}.{duckImplementationClassName}({createMergedConstructorArguments});
        }}
";

            }
        }

        private static IEnumerable<ISymbol> CreateConstructorArguments(ITypeSymbol type)
        {
            return type.GetAllMembers()
                .Where(o => o.DeclaredAccessibility.HasFlag(Accessibility.Public))
                .SelectMany(CreateArgumentsIterator)
                .ToArray();

            IEnumerable<ISymbol> CreateArgumentsIterator(ISymbol symbol)
            {
                switch (symbol)
                {
                    case IPropertySymbol property:
                    {
                        yield return property;
                        break;
                    }
                    case IMethodSymbol { MethodKind: MethodKind.Ordinary } method:
                    {
                        yield return method;
                        break;
                    }
                }
            }
        }

        private static IEnumerable<(string Type, string Name)> FormatMakeArguments(ISymbol symbol)
        {
            switch (symbol)
            {
                case IPropertySymbol property:
                {
                    var name = property.Name.LowerFirstChar();
                    if (property.GetMethod is not null)
                    {
                        yield return ($"Func<{property.Type.ToGlobalName()}>", $"{name}Getter");
                    }

                    if (property.SetMethod is not null)
                    {
                        yield return ($"Action<{property.Type.ToGlobalName()}>", $"{name}Setter");
                    }

                    break;
                }
                case IMethodSymbol { MethodKind: MethodKind.Ordinary } method:
                {
                    var name = method.Name.LowerFirstChar();
                    if (method.ReturnsVoid)
                    {
                        yield return ($"Action<{method.Parameters.Select(o => o.Type.ToGlobalName()).Join()}>", $"{name}");
                    }
                    else
                    {
                        var types = new[] { method.ReturnType.ToGlobalName() }.Concat(method.Parameters.Select(o => o.Type.ToGlobalName()));
                        yield return ($"Func<{types.Join()}>", $"{name}");
                    }

                    break;
                }
            }
        }

        private static IEnumerable<string> CreateConstructorBody(ITypeSymbol duckInterface)
        {
            return duckInterface
                .GetAllMembers()
                .Where(o => o.DeclaredAccessibility.HasFlag(Accessibility.Public))
                .SelectMany(CreateBodyIterator)
                .ToArray();

            IEnumerable<string> CreateBodyIterator(ISymbol symbol)
            {

                switch (symbol)
                {
                    case IPropertySymbol property:
                    {
                        var name = property.Name.LowerFirstChar();
                        if (property.GetMethod is not null)
                        {
                            yield return $"_{property.Name}Getter = {name}Getter";
                        }

                        if (property.SetMethod is not null)
                        {
                            yield return $"_{property.Name}Setter = {name}Getter";
                        }

                        break;
                    }
                    case IMethodSymbol
                        {
                            MethodKind:
                            MethodKind.Ordinary
                        }
                        method:
                    {
                        var name = method.Name.LowerFirstChar();
                        yield return $"_{method.Name} = {name}";
                        break;
                    }
                }
            }

        }

        private static IEnumerable<string> CreateSourceForProperties(ITypeSymbol duckInterface)
        {
            return duckInterface
                .GetAllMembers()
                .OfType<IPropertySymbol>()
                .Select(property => $@"
        public {property.Type.ToGlobalName()} {property.Name}
        {{
            {(property.GetMethod != null ? $" [System.Diagnostics.DebuggerStepThrough] get {{ return _{property.Name}Getter(); }}" : string.Empty)}
            {(property.SetMethod != null ? $" [System.Diagnostics.DebuggerStepThrough] set {{ _{property.Name}Setter(value); }}" : string.Empty)}
        }}
");
        }

        private static IEnumerable<string> CreateSourceForMethods(ITypeSymbol duckInterface)
        {

            return duckInterface
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
        }

        private static IEnumerable<string> CreateSourceForFields(
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