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
        
        public static async Task<Project> ApplyDuckGenerators(this Project project)
        {
            var newProject = await project.RunSourceGenerator(new DuckSourceInvocationGenerator(), new DuckSyntaxInvocationReceiver());
            newProject = await newProject.RunSourceGenerator(new DuckSourceInterfaceGenerator(), new DuckSyntaxInterfaceReceiver());

            return newProject;
        }
        
        public static async Task<Project> RunSourceGenerator<TGenerator>(this Project project, TGenerator generator,
            ISyntaxReceiver syntaxReceiver)
            where TGenerator : ISourceGenerator
        {
            if (syntaxReceiver != null)
            {
                var nodes = project.Documents
                    .Select(o => o.GetSyntaxTreeAsync().Result)
                    .SelectMany(o => o.GetRoot().DescendantNodes());

                foreach (var syntaxNode in nodes)
                {
                    syntaxReceiver.OnVisitSyntaxNode(syntaxNode);
                }
            }

            var nonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
            var compilation = await project.GetCompilationAsync();
            var context = (GeneratorExecutionContext) Activator.CreateInstance(
                type: typeof(GeneratorExecutionContext), nonPublic | BindingFlags.CreateInstance, null, new object[]
                {
                    compilation,
                    project.ParseOptions,
                    ImmutableArray.Create<AdditionalText>(),
                    null,
                    syntaxReceiver,
                    CancellationToken.None
                }, null)!;

            generator.Execute(context);

            var files = context
                .ReflectionGetValue("_additionalSources")
                .ReflectionGetValue("_sourcesAdded") as IEnumerable;

            foreach (var file in files)
            {
                project = project.AddDocument((string) file.ReflectionGetValue("HintName"),
                    (SourceText) file.ReflectionGetValue("Text")).Project;
            }

            return project;
        }

        public static object ReflectionGetValue(this object @object, string name)
        {
            var nonPublic = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var member = @object.GetType().GetField(name, nonPublic);
            if (member is null)
            {
                return @object
                    .GetType()
                    .GetProperty(name, nonPublic)
                    ?.GetValue(@object);
            }

            return member.GetValue(@object);
        }

        public static object ReflectionCall(this object @object, string name)
        {
            var nonPublic = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var member = @object.GetType().GetField(name, nonPublic);
            if (member is null)
            {
                return @object
                    .GetType()
                    .GetProperty(name, nonPublic)
                    ?.GetValue(@object);
            }

            return member.GetValue(@object);
        }
    }
}