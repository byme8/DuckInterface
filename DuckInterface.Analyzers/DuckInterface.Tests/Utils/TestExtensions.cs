﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DuckInterface.Test.Utils
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

        public static async Task<Assembly> CompileToRealAssembly(this Project project)
        {
            var compilation = await project.GetCompilationAsync();
            var analyzerResults = compilation.GetDiagnostics();

            var error = compilation.GetDiagnostics().Concat(analyzerResults)
                .FirstOrDefault(o => o.Severity == DiagnosticSeverity.Error);

            if (error != null)
            {
                throw new Exception(error.GetMessage());
            }

            using (var memoryStream = new MemoryStream())
            {
                compilation.Emit(memoryStream);
                var bytes = memoryStream.ToArray();
                var assembly = Assembly.Load(bytes);

                return assembly;
            }
        }
    }
}