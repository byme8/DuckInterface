using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DuckInterface.Analyzers
{
    public class DuckExtensionsSyntaxReceiver : ISyntaxReceiver
    {
        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is InvocationExpressionSyntax invocation &&
                invocation.Expression is MemberAccessExpressionSyntax member &&
                member.Name.Identifier.ToString() is "Duck" or "From")
            {
                this.MemberAccesses.Add(member);
            }
        }

        public List<MemberAccessExpressionSyntax> MemberAccesses { get; } = new();
    }
}