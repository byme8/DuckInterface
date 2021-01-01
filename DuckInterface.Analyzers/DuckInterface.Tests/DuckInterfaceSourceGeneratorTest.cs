using System.Linq;
using System.Threading.Tasks;
using DuckInterface.Test.Data;
using DuckInterface.Test.Units;
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

            var newProject = await project.ApplyDuckGenerators();

            var compilation = await newProject.GetCompilationAsync();
            var errors = compilation.GetDiagnostics()
                .Where(o => o.Severity == DiagnosticSeverity.Error)
                .ToArray();

            Assert.IsFalse(errors.Any(), errors.Select(o => o.GetMessage()).JoinWithNewLine());
        }

        [TestMethod]
        public async Task DoesntWorkForUnduckableType()
        {
            var project = await TestProject.Project.ReplacePartOfDocumentAsync(
                "Program.cs",
                "public float Calculate(float a, float b)",
                "public float CalculateValue(float a, float b)");

            var newProject = await project.ApplyDuckGenerators();

            var compilation = await newProject.GetCompilationAsync();
            var diagnostics = compilation
                .GetDiagnostics()
                .Where(o => o.Severity == DiagnosticSeverity.Error)
                .ToArray();

            Assert.IsTrue(
                diagnostics.Any(o =>o.GetMessage() == "Argument 1: cannot convert from 'TestProject.AddCalculator' to 'TestProject.DICalculator'"),
                "Type with different public interface should be ignored.");
        }

        [TestMethod]
        public async Task DeepNamespacesWorks()
        {
            var project = await TestProject.Project.ReplacePartOfDocumentAsync(
                "Program.cs",
                "namespace TestProject",
                "namespace TestProject.SomeNamespace.Test");

            var newProject = await project.ApplyDuckGenerators();

            var compilation = await newProject.GetCompilationAsync();
            var diagnostics = compilation
                .GetDiagnostics()
                .Where(o => o.Severity == DiagnosticSeverity.Error)
                .ToArray();

            Assert.IsFalse(diagnostics.Any(), "Long namespaces doesn't work.");
        }

        [TestMethod]
        public async Task NewStatementDetected()
        {
            var project = await TestProject.Project.ReplacePartOfDocumentAsync(
                "Program.cs",
                "Doit(calculator);",
                "Doit(new AddCalculator());");

            var newProject = await project.ApplyDuckGenerators();

            var compilation = await newProject.GetCompilationAsync();
            var diagnostics = compilation
                .GetDiagnostics()
                .Where(o => o.Severity == DiagnosticSeverity.Error)
                .ToArray();

            Assert.IsFalse(diagnostics.Any(), "New statement failed.");
        }

        [TestMethod]
        public async Task ArgumentDetected()
        {
            var project = await TestProject.Project.ReplacePartOfDocumentAsync(
                "Program.cs",
                "Doit(calculator);",
                @"Doit2(calculator);");

            project = await project.ReplacePartOfDocumentAsync(
                "Program.cs",
                "// additional 2",
                @"
                    public static void Doit3(AddCalculator calculator)
                    {
                        Doit(calculator);
                    }");

            var newProject = await project.ApplyDuckGenerators();

            var compilation = await newProject.GetCompilationAsync();
            var diagnostics = compilation
                .GetDiagnostics()
                .Where(o => o.Severity == DiagnosticSeverity.Error)
                .ToArray();

            Assert.IsFalse(diagnostics.Any(), "Argument doesn't work.");
        }

        [TestMethod]
        public async Task AssigmentDetected()
        {
            var toReplace = new[]
            {
                ("Doit(calculator); // Main",
                  @"
                  DICalculator dcalculator1 = calculator;
                  DICalculator dcalculator2 = new AddCalculator();
                  var container = new Container { Calculator = calculator };"
                ),
                ("// additional 1", "/*"),
                ("// additional 2", "*/")
            };

            var updatedProject = toReplace.Aggregate(TestProject.Project,
                (project, tuple) => project.ReplacePartOfDocumentAsync("Program.cs", tuple.Item1, tuple.Item2).Result);

            var newProject = await updatedProject.ApplyDuckGenerators();

            var compilation = await newProject.GetCompilationAsync();
            var diagnostics = compilation
                .GetDiagnostics()
                .Where(o => o.Severity == DiagnosticSeverity.Error)
                .ToArray();

            Assert.IsFalse(diagnostics.Any(), diagnostics.Select(o => o.GetMessage()).JoinWithNewLine());
        }
        
        [TestMethod]
        public async Task CountsForMissingProperties()
        {
            var toReplace = new[]
            {
                ("public float Value { get; set; } //", @"" ),
            };

            var updatedProject = toReplace.Aggregate(TestProject.Project,
                (project, tuple) => project.ReplacePartOfDocumentAsync("Program.cs", tuple.Item1, tuple.Item2).Result);

            var newProject = await updatedProject.ApplyDuckGenerators();

            var compilation = await newProject.GetCompilationAsync();
            var diagnostics = compilation
                .GetDiagnostics()
                .Where(o => o.Severity == DiagnosticSeverity.Error)
                .ToArray();

            Assert.IsTrue(
                diagnostics.Any(o =>o.GetMessage() == "Argument 1: cannot convert from 'TestProject.AddCalculator' to 'TestProject.DICalculator'"),
                "Type with different public interface should be ignored.");
        }
    }
}