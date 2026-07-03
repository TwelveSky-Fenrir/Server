#if NETSTANDARD2_0
// Lets this netstandard2.0 generator use `init`/`required` internally: the compiler only checks
// these types' presence/shape, not their origin. Never affects emitted code (targets net10.0,
// where they exist natively).
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit;
}
#endif
