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
        
        static TestProject()
        {
            var workspace = new AdhocWorkspace();
            Project = workspace
                .AddProject("TestProject", LanguageNames.CSharp)
                .WithMetadataReferences(GetReferences())
                .AddDocument("Program.cs", ProgramCS).Project;
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
                MetadataReference.CreateFromFile(typeof(DuckAttribute).Assembly.Location),
            };
        }
        
        public const string ProgramCS = @"
using System;

namespace TestProject 
{
    [Duck]
    public partial struct ICalculator
    {
        public partial float Calculate(float a, float b);
    }

    public class AddCalculator
    {
        public float Calculate(float a, float b)
        {
            return a + b;
        }
    }

    public static class Program 
    {
        public static void Main(string[] args)
        {
            var calculator = new AddCalculator();
            Doit(calculator);
        }

        public static void Doit(ICalculator calculator)
        {
            var result = calculator.Calculate(1, 2);
        }
    }   
}
";
    }
}