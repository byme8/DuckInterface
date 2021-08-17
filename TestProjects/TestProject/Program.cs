using System;
using System.IO;

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

    public class Container
    {
        public DICalculator Calculator { get; set; }
    }

    [Duckable]
    public interface IStream
    {
        int ReadByte();
    }

    class Program
    {
        static void Main(string[] args)
        {
            var memoryStream = new MemoryStream();
            UseStream(memoryStream);
            
            var calculator = new AddCalculator();
            Doit(calculator); // Main
        }

        public static void UseStream(DIStream stream)
        {
            
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