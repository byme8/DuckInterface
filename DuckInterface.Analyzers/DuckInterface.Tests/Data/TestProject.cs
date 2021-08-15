using System;
using System.Buffers;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace DuckInterface.Test.Data
{
    public static class TestProject
    {
        public static Project Project { get; }
        
        public static AdhocWorkspace Workspace { get; }
        
        static TestProject()
        {
            Workspace = new AdhocWorkspace();
            Workspace = Workspace
                .AddProject("TestProjectLibrary", LanguageNames.CSharp)
                .WithMetadataReferences(GetReferences())
                .AddDocument("Library.cs", Library).Project.Solution.Workspace as AdhocWorkspace;
            
            var libraryProject = Workspace.CurrentSolution.Projects.First();
            
            Workspace = Workspace
                .AddProject("TestProject", LanguageNames.CSharp)
                .WithMetadataReferences(GetReferences())
                .WithProjectReferences(new []{new ProjectReference(libraryProject.Id)})
                .AddDocument("Program.cs", ProgramCS).Project.Solution.Workspace as AdhocWorkspace;

            Project = Workspace.CurrentSolution.Projects.Last();
        }
        
        
        private static MetadataReference[] GetReferences()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            return new MetadataReference[]
            {
                MetadataReference.CreateFromFile(assemblies.Single(a => a.GetName().Name == "netstandard").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Buffers").Location),
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ArrayPool<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(DuckableAttribute).Assembly.Location),
            };
        }

        public const string Library = @"
using System;

namespace TestProject 
{
    [Duckable]
    public interface ICalculator
    {
        float Value { get; set; } 
        float ValueGet { get; }
        float ValueSet { set; } 
        float Calculate(float a, float b);
        void Save();
        void Do(System.Threading.CancellationToken token); 
        byte[] ToArray();
    }
}
";
        
        public const string ProgramCS = @"
using System;

namespace TestProject 
{
    public class BaseCalculator 
    {
        public float ValueGet { get; }
    }

    public class AddCalculator : BaseCalculator
    {
        public float Value { get; set; } //
        public float ValueSet
        { 
            set 
            {
            } 
        } 

        public float Calculate(float a, float b)
        {
            return a + b;
        }

        public void Save()
        {

        }

        public void Do(System.Threading.CancellationToken token) 
        {
        }

        public byte[] ToArray()
        {
            return null;
        }
    }

    public class Container
    {
        public DICalculator Calculator { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var calculator = new AddCalculator();
            Doit(calculator); // Main
        }

        // additional 1

        public static float Doit(DICalculator calculator)
        {
            var @interface = DuckToInterface(calculator); 
            return calculator.Calculate(10, calculator.Calculate(10, 20));
        }

        public static float Doit2(AddCalculator calculator)
        {
            return Doit(calculator);
        }

        public static float DuckToInterface(ICalculator calculator)
        {
            return calculator.Calculate(10, calculator.Calculate(10, 20));
        }

        // additional 2
    }   
}
";
    }
}