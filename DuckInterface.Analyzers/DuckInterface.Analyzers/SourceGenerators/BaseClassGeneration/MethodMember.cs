using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace DuckInterface.Analyzers.SourceGenerators.BaseClassGeneration;

public class MethodMember : BaseMember
{
    private readonly IMethodSymbol method;

    public MethodMember(IMethodSymbol method)
    {
        this.method = method;
    }

    public override string SafeName => method.Name;
    public override string ArgumentName => $"@{method.Name.LowerFirstChar()}";
    public override string MemberType => BackingFieldTypes.First();
    public override string Member
    {
        get
        {
            var returnType = method.ReturnType;
            var parameters = method.Parameters;
            
            return @$"
        [global::System.Diagnostics.DebuggerStepThrough]
        {returnType.ToGlobalName()} {method.ContainingType.ToGlobalName()}.{SafeName}({parameters.Select(o => $"{o.Type.ToGlobalName()} {o.Name}").Join()})
        {{
            {(returnType.SpecialType == SpecialType.System_Void ? "" : "return ")}{BackingFieldNames.First()}({parameters.Select(o => o.Name).Join()});
        }}
";
        }
    }

    public override IEnumerable<string> BackingFieldNames
    {
        get { yield return $"_{SafeName}"; }
    }

    public override IEnumerable<string> BackingFieldArgumentNames
    {
        get { yield return ArgumentName; }
    }

    public override IEnumerable<string> BackingFieldTypes
    {
        get
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

            var types = arguments
                .Select(o => o.Type.ToGlobalName())
                .Concat(new[] { @return.ReturnType })
                .Where(o => !string.IsNullOrEmpty(o));

            yield return $@"{@return.FuncOrAction}{types.Join().Wrap(wrappers.Left, wrappers.Right)}";
        }
    }

    public override IEnumerable<string> BackingField
    {
        get
        {
            return BackingFieldTypes.Zip(BackingFieldNames, (type, name) => $@"
        [global::System.Diagnostics.DebuggerBrowsable(global::System.Diagnostics.DebuggerBrowsableState.Never)] 
        private readonly {type} {name};");
        }
    }

    public override IEnumerable<string> CreateLambdas
    {
        get { yield return ArgumentName; }
    }
}