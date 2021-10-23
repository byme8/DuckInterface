using System;

namespace DuckInterface
{
    public interface IDuckErrorsProvider
    {
        string[] Errors { get; }
    }

    public static class DuckExtensions
    {
        public static TInterface Duck<TInterface>(this object instance)
            where TInterface : class
        {
            throw new NotImplementedException($"Source generation failed to run for type {instance.GetType().FullName}");
        }
    }
}