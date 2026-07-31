#if NETSTANDARD2_0
// Polyfill so this netstandard2.0 generator can use `init`/`required`; compiler only checks type shape, not origin.
namespace Fenrir.Generators.Analysis.Support
{
    internal static class IsExternalInit;
}
#endif
