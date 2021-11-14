using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace DuckInterface.Analyzers.SourceGenerators.BaseClassGeneration;

public class PropertyMember : BaseMember
{
    private readonly IPropertySymbol property;

    public PropertyMember(IPropertySymbol property)
    {
        this.property = property;
    }

    public override string SafeName
    {
        get
        {
            if (!property.IsIndexer)
            {
                return property.Name;
            }

            return $"Indexer_{property.Parameters.First().Type.ToSafeGlobalName()}";
        }
    }

    public override string ArgumentName
        => $"@{SafeName.LowerFirstChar()}";

    public override string MemberType
        => property.Type.ToGlobalName();

    public override string Member
    {
        get
        {
            if (property.IsIndexer)
            {
                return $@"
                        {MemberType} {property.ContainingType.ToGlobalName()}.this[{property.Parameters.First().Type.ToGlobalName()} i]
                        {{
                            {(property.GetMethod != null ? $" [global::System.Diagnostics.DebuggerStepThrough] get {{ return {Getter}(i); }}" : string.Empty)}
                            {(property.SetMethod != null ? $" [global::System.Diagnostics.DebuggerStepThrough] set {{ {Setter}(i, value); }}" : string.Empty)}
                        }}
";
            }

            return $@"
        {MemberType} {property.ContainingType.ToGlobalName()}.{SafeName}
        {{
            {(property.GetMethod != null ? $" [global::System.Diagnostics.DebuggerStepThrough] get {{ return {Getter}(); }}" : string.Empty)}
            {(property.SetMethod != null ? $" [global::System.Diagnostics.DebuggerStepThrough] set {{ {Setter}(value); }}" : string.Empty)}
        }}
";
        }
    }

    public string Getter => $"_{SafeName}Getter";
    public string Setter => $"_{SafeName}Setter";

    public override IEnumerable<string> BackingFieldNames
    {
        get
        {
            if (property.GetMethod != null)
            {
                yield return Getter;
            }

            if (property.SetMethod != null)
            {
                yield return Setter;
            }
        }
    }

    public override IEnumerable<string> BackingFieldArgumentNames
    {
        get
        {
            if (property.GetMethod != null)
            {
                yield return $"{ArgumentName}Getter";
            }

            if (property.SetMethod != null)
            {
                yield return $"{ArgumentName}Setter";
            }
        }
    }

    public override IEnumerable<string> BackingFieldTypes
    {
        get
        {
            var propertyArguments = property.Parameters.Any() ? $"{property.Parameters.First().Type.ToGlobalName()}, " : string.Empty;
            if (property.GetMethod != null)
            {
                yield return $"Func<{propertyArguments}{property.Type.ToGlobalName()}>";
            }

            if (property.SetMethod != null)
            {
                yield return $"Action<{propertyArguments}{property.Type.ToGlobalName()}>";
            }
        }
    }

    public override IEnumerable<string> BackingField =>
        BackingFieldTypes.Zip(BackingFieldNames, (type, name) =>
            $@"            [global::System.Diagnostics.DebuggerBrowsable(global::System.Diagnostics.DebuggerBrowsableState.Never)] 
            private readonly {type} {name};");

    public string GetLambda
        => $"() => {ArgumentName}";

    public string SetLambda
        => $"o => {ArgumentName} = o";

    public override IEnumerable<string> CreateLambdas
    {
        get
        {
            if (property.GetMethod != null)
            {
                yield return GetLambda;
            }

            if (property.SetMethod != null)
            {
                yield return SetLambda;
            }
        }
    }
}