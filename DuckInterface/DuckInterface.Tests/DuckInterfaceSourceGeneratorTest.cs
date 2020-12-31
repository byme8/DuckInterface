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
            var error = compilation.GetDiagnostics().Any(o => o.Severity == DiagnosticSeverity.Error);
            
            Assert.IsFalse(error);
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
            
            Assert.IsTrue(diagnostics
                .Any(o => o.GetMessage() == "Argument 1: cannot convert from 'TestProject.AddCalculator' to 'TestProject.ICalculator'"));
        }
    }
}
