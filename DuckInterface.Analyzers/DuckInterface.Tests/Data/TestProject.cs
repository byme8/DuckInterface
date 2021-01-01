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

    public class AddCalculator
    {
        public float Value { get; set; } //
        public float ValueGet { get; }
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