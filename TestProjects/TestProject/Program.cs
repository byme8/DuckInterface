using System.Collections.Generic;
using System.IO;
using DuckInterface;
// namespaces

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

    public interface IStream<TData>
    {
        TData Read();
    }
    
    public interface IStream
    {
        int ReadByte();
    }

    public interface IStreamConfig
    {
        bool CanSeek { get; }
        bool CanRead { get; }
        bool CanWrite { get; }
    }

    class Program
    {
        public static void Main(string[] args)
        {
            // main
        }

        public static IStream GetStream()
        {
            var stream = new MemoryStream();
            return stream.Duck<IStream>();
        }

        // additional 1
    }
}