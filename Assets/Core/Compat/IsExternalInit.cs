#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    /// <summary>Polyfill so C# 9 records / init-only setters compile on netstandard2.1 (Unity 6).</summary>
    internal static class IsExternalInit { }
}
#endif
