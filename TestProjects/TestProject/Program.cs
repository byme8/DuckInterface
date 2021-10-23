using System;
using System.IO;
using DuckInterface;

namespace TestProject
{
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

    public interface IStream
    {
        int ReadByte();
    }

    class Program
    {
        static void Main(string[] args)
        {
            // main
            var stream = new MemoryStream();
            UseStream(stream.Duck<IStream>());
        }

        public static void UseStream(IStream stream)
        {
        }
        
        // additional 1
    }
}