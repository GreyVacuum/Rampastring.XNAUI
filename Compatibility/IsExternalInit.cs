#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Polyfill for the compiler-required <c>IsExternalInit</c> type, which is needed for
    /// <c>init</c>-only setters and records but is not present in .NET Framework / .NET Standard.
    /// Only compiled for target frameworks older than .NET 5.
    /// </summary>
    internal static class IsExternalInit { }
}
#endif
