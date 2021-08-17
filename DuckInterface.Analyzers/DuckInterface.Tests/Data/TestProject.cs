using System.Linq;
using Buildalyzer;
using Buildalyzer.Workspaces;
using Microsoft.CodeAnalysis;

namespace DuckInterface.Test.Data
{
    public static class TestProject
    {
        public static Project Project { get; }

        public static AdhocWorkspace Workspace { get; }

        static TestProject()
        {
            var manager = new AnalyzerManager();
            manager.GetProject(@"../../../../../TestProjects/TestProject/TestProject.csproj");
            Workspace = manager.GetWorkspace();

            Project = Workspace.CurrentSolution.Projects.First(o => o.Name == "TestProject");
        }
    }
}