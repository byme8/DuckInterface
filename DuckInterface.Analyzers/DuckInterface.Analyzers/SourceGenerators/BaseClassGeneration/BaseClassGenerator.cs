using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace DuckInterface.Analyzers.SourceGenerators.BaseClassGeneration
{
    public static class BaseClassGeneratorv2
    {
        class BaseClassModel
        {
            public BaseMember[] Members { get; set; }
            public string ContainingNamespace { get; set; }
            public string DuckInterfaceGlobalName { get; set; }
            public string DuckImplementationClassName { get; set; }
            public string DuckInterfaceSafeGlobalName { get; set; }
        }

        public static void Generate(GeneratorExecutionContext context, ITypeSymbol duckInterface)
        {
            var model = MakeModel(context, duckInterface);
            if (!model.Members.Any())
            {
                return;
            }

            var source = Render(model);
            context.AddSource(model.DuckImplementationClassName, source.ToSourceText());
        }

        private static BaseClassModel MakeModel(GeneratorExecutionContext context, ITypeSymbol duckInterface)
        {
            var @namespace = GetNamespace(duckInterface);
            var properties = duckInterface
                .GetAllMembers()
                .OfType<IPropertySymbol>()
                .Select(o => new PropertyMember(o));


            var methods = duckInterface
                .GetAllMembers()
                .GetPublicMethods()
                .Select(o => new MethodMember(o));

            var model = new BaseClassModel
            {
                ContainingNamespace = @namespace.ToString(),
                DuckInterfaceGlobalName = duckInterface.ToGlobalName(),
                DuckInterfaceSafeGlobalName = duckInterface.ToSafeGlobalName(),
                DuckImplementationClassName = Utils.GetDuckImplementationClassName(duckInterface),
                Members = properties.Concat<BaseMember>(methods).ToArray()
            };

            return model;
        }

        private static string Render(BaseClassModel model)
            => $@"using System;
using DuckInterface.Core;

namespace DuckInterface
{{
    public static class DuckHandlerExtensionsFor{model.DuckInterfaceSafeGlobalName}
    {{
        
        public static {model.DuckInterfaceGlobalName} Create(this IDuckHandler<{model.DuckInterfaceGlobalName}> handler, {model.Members.Select(o => $"{o.MemberType} {o.ArgumentName}").Join()})
        {{
            return new {model.ContainingNamespace}.{model.DuckImplementationClassName}({model.Members.SelectMany(o => o.CreateLambdas).Join()});
        }}

        public static {model.DuckInterfaceGlobalName} CreatePartial(this IDuckHandler<{model.DuckInterfaceGlobalName}> handler, {model.Members.Select(o => $"{o.MemberType} {o.ArgumentName} = default").Join()})
        {{
            return new {model.ContainingNamespace}.{model.DuckImplementationClassName}({model.Members.SelectMany(o => o.CreateLambdas).Join()});
        }}

        public static {model.DuckInterfaceGlobalName} Make(this IDuckHandler<{model.DuckInterfaceGlobalName}> handler, {model.Members.SelectMany(o => o.BackingFieldTypes.Zip(o.BackingFieldArgumentNames, (type, name) => $"{type} {name}")).Join()})
        {{
            return new {model.ContainingNamespace}.{model.DuckImplementationClassName}({model.Members.SelectMany(o => o.BackingFieldArgumentNames).Join()});
        }}

        public static {model.DuckInterfaceGlobalName} MakePartial(this IDuckHandler<{model.DuckInterfaceGlobalName}> handler, {model.Members.SelectMany(o => o.BackingFieldTypes.Zip(o.BackingFieldArgumentNames, (type, name) => $"{type} {name} = default")).Join()})
        {{
            return new {model.ContainingNamespace}.{model.DuckImplementationClassName}({model.Members.SelectMany(o => o.BackingFieldArgumentNames).Join()});
        }}
    }}
}}

namespace {model.ContainingNamespace} 
{{
    {Utils.EditorBrowsable}
    public partial class {model.DuckImplementationClassName}: {model.DuckInterfaceGlobalName} 
    {{
        public {model.DuckImplementationClassName}({model.Members.SelectMany(o => o.BackingFieldTypes.Zip(o.BackingFieldArgumentNames, (type, name) => $"{type} {name}")).Join()})
        {{
{model.Members.SelectMany(o => o.BackingFieldNames.Zip(o.BackingFieldArgumentNames, (fieldName, name) => $"            {fieldName} = {name}")).JoinWithNewLine(";")};
        }}
{model.Members.SelectMany(o => o.BackingField).JoinWithNewLine()}        
{model.Members.Select(o => o.Member).JoinWithNewLine()}        
    }}
}}
";

        private static StringBuilder GetNamespace(ITypeSymbol duckInterface)
        {
            var containingNamespace = duckInterface.ContainingNamespace.ToDisplayString(new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces));
            var @namespace = new StringBuilder();
            @namespace.Append("DuckInterface.Generated");
            if (!string.IsNullOrEmpty(containingNamespace))
            {
                @namespace.Append(".");
                @namespace.Append(containingNamespace);
            }

            return @namespace;
        }
    }
}