using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DuckInterface
{
    public static class Units
    {
        public static bool IsTypeDuckableTo(this ITypeSymbol @interface, ITypeSymbol implementation)
        {
            var methodsToDuck = @interface
                .GetMembers()
                .OfType<IMethodSymbol>()
                .Where(o => o.Name != ".ctor")
                .Select(o => 
                    o.ReturnType.ToGlobalName() + 
                    o.Name + 
                    o.Parameters
                        .Select(oo => oo.Type.ToGlobalName() + oo.Name)
                        .Join())
                .ToArray();

            var memberThatCanBeDucked = implementation
                .GetMembers()
                .OfType<IMethodSymbol>()
                .Select(o =>
                    o.ReturnType.ToGlobalName() + 
                    o.Name + 
                    o.Parameters
                        .Select(oo => oo.Type.ToGlobalName() + oo.Name)
                        .Join())
                .ToImmutableHashSet();

            var canBeDuck = methodsToDuck
                .All(o => memberThatCanBeDucked.Contains(o));


            return canBeDuck;
        }
        
        public static string GetUniqueName(this ITypeSymbol type)
        {
            return $"{type.Name}_{Guid.NewGuid().ToString().Replace("-", "")}";
        }
        
        public static SourceText ToSourceText(this string source)
        {
            return SourceText.From(source, Encoding.UTF8);
        }
        
        public static string ToGlobalName(this ISymbol symbol)
        {
            return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        public static string Join(this IEnumerable<string> values, string separator = ", ")
        {
            return string.Join(separator, values);
        }

        public static string JoinWithNewLine(this IEnumerable<string> values, string separator = "")
        {
            return string.Join($"{separator}{Environment.NewLine}", values);
        }
    }
}