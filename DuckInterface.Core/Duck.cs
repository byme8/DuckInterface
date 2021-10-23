using DuckInterface.Core;

namespace DuckInterface
{
    public static class Duck
    {
        public static IDuckHandler<T> From<T>()
        {
            return default;
        }
    }
}