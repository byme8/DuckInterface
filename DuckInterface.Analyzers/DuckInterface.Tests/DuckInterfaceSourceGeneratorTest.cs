using System;
using System.Linq;
using System.Threading.Tasks;
using DuckInterface.Test.Data;
using DuckInterface.Test.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DuckInterface.Test
{
    [TestClass]
    public class DuckInterfaceUnitTest
    {
        [TestMethod]
        public async Task CompilesWithoutErrors()
        {
            var project = TestProject.Project;

            var compilation = await project.GetCompilationAsync();
            var errors = compilation.GetDiagnostics()
                .Where(o => o.Severity == DiagnosticSeverity.Error)
                .ToArray();

            Assert.IsFalse(errors.Any(), errors.Select(o => o.GetMessage()).JoinWithNewLine());
        }

        [TestMethod]
        public async Task DoesntWorkForUnduckableType()
        {
            var errorMessage = "Duck mapping between 'IStream' and 'MemoryStream' is not possible. Next members are missing: ReadMissingByte";
            var project = await TestProject.Project.ReplacePartOfDocumentAsync(
                "Program.cs",
                "int ReadByte();",
                "int ReadMissingByte();");

            var errors = await CompileAndExtractErrors(project);

            Assert.IsTrue(errors.First() == errorMessage, "Type with different public interface should be ignored.");
        }

        private static async Task<string[]> CompileAndExtractErrors(Project project)
        {
            var assembly = await project.CompileToRealAssembly();
            var errorProviderType = assembly.GetType("DuckInterface.Generated.DuckErrors");
            if (errorProviderType is null)
            {
                return Enumerable.Empty<string>().ToArray();
            }

            var errorProvider = Activator.CreateInstance(errorProviderType) as IDuckErrorsProvider;
            return errorProvider!.Errors;
        }

        [TestMethod]
        public async Task DeepNamespacesWorks()
        {
            var project = await TestProject.Project.ReplacePartOfDocumentAsync(
                "Program.cs",
                "namespace TestProject",
                "namespace TestProject.SomeNamespace.Test");

            var errors = await CompileAndExtractErrors(project);
            Assert.IsFalse(errors.Any(), "Long namespaces doesn't work.");
        }

        [TestMethod]
        public async Task DuckFromWorks()
        {
            var project = await TestProject.Project.ReplacePartOfDocumentAsync(
                "Program.cs",
                "// main",
                "Duck.From<IStreamConfig>().Make(() => true, () => true, () => true);");

            var errors = await CompileAndExtractErrors(project);
            Assert.IsFalse(errors.Any(), "Long namespaces doesn't work.");
        }

        [TestMethod]
        public async Task PartialDuckFromWorks()
        {
            var project = await TestProject.Project.ReplacePartOfDocumentAsync(
                "Program.cs",
                "// main",
                "Duck.From<IStreamConfig>().MakePartial(() => true);");

            var errors = await CompileAndExtractErrors(project);
            Assert.IsFalse(errors.Any(), "Long namespaces doesn't work.");
        }
    }
}