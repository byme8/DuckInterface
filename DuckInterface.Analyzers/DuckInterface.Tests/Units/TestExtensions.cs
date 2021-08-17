using System;
using System.Collections;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DuckInterface.Test.Units
{
    public static class TestExtensions
    {
        public static async Task<Project> ReplacePartOfDocumentAsync(this Project project, string documentName, string textToReplace, string newText)
        {
            var document = project.Documents.First(o => o.Name == documentName);
            var text = await document.GetTextAsync();
            return document
                .WithText(SourceText.From(text.ToString().Replace(textToReplace, newText)))
                .Project;
        }
    }
}