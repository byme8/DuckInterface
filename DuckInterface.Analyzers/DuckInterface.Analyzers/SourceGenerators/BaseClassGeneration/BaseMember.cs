using System.Collections.Generic;

namespace DuckInterface.Analyzers.SourceGenerators.BaseClassGeneration;

public abstract class BaseMember
{
    public abstract string SafeName { get; }
    public abstract string ArgumentName { get; }
    public abstract string Member { get; }
    public abstract string MemberType { get; }
    public abstract IEnumerable<string> BackingFieldNames { get; }
    public abstract IEnumerable<string> BackingFieldArgumentNames { get; }
    public abstract IEnumerable<string> BackingFieldTypes { get; }
    public abstract IEnumerable<string> BackingField { get; }
    public abstract IEnumerable<string> CreateLambdas { get; }
}